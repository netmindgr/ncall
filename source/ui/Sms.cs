// nCall SMS: sending through the customer's own SMS provider account. GPL-3.0-or-later.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace Netmind.UI {

internal enum SmsState { Pending, Delivered, Failed }

internal sealed class SmsSent {
    internal string Id,Phone;
    internal int Parts;
}

internal sealed class SmsException : Exception {
    internal SmsException(string message):base(message){}
}

// One SMS provider: what the customer enters and the three API operations.
internal interface ISmsProvider {
    string Id{get;}
    string Name{get;}
    string UserLabel{get;}      // null when the provider needs a single key
    string SecretLabel{get;}
    string Help{get;}           // where the customer finds the credentials
    bool HasStatus{get;}        // delivery status can be polled (otherwise only "sent")
    bool GreekCapitalsInGsm{get;} // the provider sends Greek capitals in GSM 7-bit (160 per SMS)
    string Balance(SmsSettings s);
    List<SmsSent> Send(SmsSettings s,IList<string> phones,string text);
    Dictionary<string,SmsState> Status(SmsSettings s,IList<string> ids);
    void Forget();
}

internal static class SmsProviders {
    internal static readonly ISmsProvider[] All={new Smsbox(),new Yuboto(),new ModulusMessaging(),new SmsTo()};
    internal static ISmsProvider Find(string id){return All.FirstOrDefault(p=>p.Id==id)??All[0];}
}

// Provider account, stored per Windows user; the secret is protected with DPAPI (CurrentUser).
internal sealed class SmsSettings {
    internal string Provider="smsbox",Username="",Password="",Sender="";
    static readonly byte[] Entropy=Encoding.UTF8.GetBytes("netmind nCall SMS");
    // SMS files live in the nCall profile folder (next to ncall.ini: the program folder when portable,
    // %APPDATA%\nCall when installed). Files from the first version's per-user folder are copied over once.
    static string Legacy{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"netmind","nCall");}}
    internal static string Folder{get{string p=Host.ProfileFolder;return String.IsNullOrEmpty(p)?Legacy:p.TrimEnd('\\','/');}}
    internal static string FileIn(string name){
        string path=Path.Combine(Folder,name),old=Path.Combine(Legacy,name);
        try{if(!File.Exists(path)&&File.Exists(old)&&!String.Equals(path,old,StringComparison.OrdinalIgnoreCase)){Directory.CreateDirectory(Folder);File.Copy(old,path);}}catch(Exception e){Host.Error(e);}
        return path;
    }
    static string FilePath{get{return FileIn("sms.txt");}}
    internal bool Complete{get{var p=SmsProviders.Find(Provider);return (p.UserLabel==null||Username!="")&&Password!=""&&Sender!="";}}
    internal static SmsSettings Load(){
        var s=new SmsSettings();
        try{
            if(!File.Exists(FilePath))return s;
            foreach(string line in File.ReadAllLines(FilePath,Encoding.UTF8)){
                int eq=line.IndexOf('=');if(eq<=0)continue;string key=line.Substring(0,eq),value=line.Substring(eq+1);
                if(key=="provider")s.Provider=value;else if(key=="username")s.Username=value;else if(key=="sender")s.Sender=value;
                else if(key=="password"&&value!="")s.Password=Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value),Entropy,DataProtectionScope.CurrentUser));
            }
        }catch(Exception e){Host.Error(e);}
        return s;
    }
    internal void Save(){
        string secret=Password==""?"":Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(Password),Entropy,DataProtectionScope.CurrentUser));
        Directory.CreateDirectory(Folder);
        File.WriteAllLines(FilePath,new[]{"provider="+Provider,"username="+Username,"sender="+Sender,"password="+secret},Encoding.UTF8);
    }
}

// HTTP helpers shared by the providers (https only, TLS 1.2/1.3).
internal static class SmsHttp {
    internal sealed class Reply { internal int Code; internal string Body=""; }
    internal static Reply Send(string method,string url,string body,string contentType,IDictionary<string,string> headers=null){
        ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12|(SecurityProtocolType)12288;
        var request=(HttpWebRequest)WebRequest.Create(url);
        request.Method=method;request.UserAgent="nCall/"+Host.Version;request.Timeout=30000;request.Accept="application/json, text/plain, */*";
        if(headers!=null)foreach(var h in headers)request.Headers[h.Key]=h.Value;
        if(body!=null){var bytes=Encoding.UTF8.GetBytes(body);request.ContentType=contentType;request.ContentLength=bytes.Length;using(var stream=request.GetRequestStream())stream.Write(bytes,0,bytes.Length);}
        HttpWebResponse response;
        try{response=(HttpWebResponse)request.GetResponse();}
        catch(WebException e){response=e.Response as HttpWebResponse;if(response==null)throw;}
        using(response)using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8))return new Reply{Code=(int)response.StatusCode,Body=reader.ReadToEnd().Trim()};
    }
    internal static string Form(params string[] pairs){
        var b=new StringBuilder();for(int i=0;i+1<pairs.Length;i+=2){if(b.Length>0)b.Append('&');b.Append(pairs[i]).Append('=').Append(Uri.EscapeDataString(pairs[i+1]??""));}return b.ToString();
    }
    internal static string Json(string value){
        var b=new StringBuilder("\"");
        foreach(char c in value??""){if(c=='"'||c=='\\')b.Append('\\').Append(c);else if(c<0x20)b.AppendFormat("\\u{0:x4}",(int)c);else b.Append(c);}
        return b.Append('"').ToString();
    }
    // Value of "name": "text" | number | true/false in a flat JSON object.
    internal static string Field(string json,string name){
        var m=Regex.Match(json??"","\""+Regex.Escape(name)+"\"\\s*:\\s*(\"((?:[^\"\\\\]|\\\\.)*)\"|[-0-9.eE+]+|true|false)");
        if(!m.Success)return "";return m.Groups[2].Success?Regex.Unescape(m.Groups[2].Value):m.Groups[1].Value;
    }
    internal static IEnumerable<string> Objects(string json){foreach(Match m in Regex.Matches(json??"","\\{[^{}]*\\}"))yield return m.Value;}
    // Full JSON parsing (nested objects/arrays) through the framework's JSON-to-XML reader:
    // object keys become elements, array entries become <item> elements.
    internal static System.Xml.Linq.XElement Parse(string json){
        if(String.IsNullOrWhiteSpace(json))return null;
        try{
            using(var reader=System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json),new System.Xml.XmlDictionaryReaderQuotas()))
                return System.Xml.Linq.XElement.Load(reader);
        }catch(Exception){return null;}
    }
    // Value at a key path, ignoring the case of the keys ("ErrorCode" / "errorCode").
    internal static string Value(System.Xml.Linq.XElement e,params string[] path){
        foreach(string key in path){if(e==null)return "";e=e.Elements().FirstOrDefault(x=>String.Equals(Key(x),key,StringComparison.OrdinalIgnoreCase));}
        return e==null?"":e.Value;
    }
    internal static IEnumerable<System.Xml.Linq.XElement> Items(System.Xml.Linq.XElement e,string key){
        var array=e==null?null:e.Elements().FirstOrDefault(x=>String.Equals(Key(x),key,StringComparison.OrdinalIgnoreCase));
        return array==null?Enumerable.Empty<System.Xml.Linq.XElement>():array.Elements();
    }
    // Keys that are not valid XML names are kept in an "item" attribute by the JSON reader.
    static string Key(System.Xml.Linq.XElement x){var a=x.Attribute("item");return a!=null?a.Value:x.Name.LocalName;}
    internal static Dictionary<string,string> Bearer(string key){return new Dictionary<string,string>{{"Authorization","Bearer "+key}};}
    internal static string Number(double value){return value.ToString("0.##",System.Globalization.CultureInfo.GetCultureInfo("el-GR"));}
    internal static string Unreachable(string provider){return "Δεν ήταν δυνατή η σύνδεση με "+provider+". Ελέγξτε τη σύνδεση στο διαδίκτυο.";}
}

// smsbox.gr HTTP API (https://www.smsbox.gr/http-sms-api.php): Batch API for sending (JSON, UCS2 keeps
// Greek lowercase), auth.php session key for balance and delivery reports.
internal sealed class Smsbox : ISmsProvider {
    const string Base="https://www.smsbox.gr/httpapi/";
    string sessionKey,sessionUser;DateTime sessionTime;
    public string Id{get{return "smsbox";}}
    public string Name{get{return "smsbox.gr";}}
    public string UserLabel{get{return "Όνομα χρήστη";}}
    public string SecretLabel{get{return "Κωδικός";}}
    public string Help{get{return "Τα στοιχεία σύνδεσης στο smsbox.gr.";}}
    public bool HasStatus{get{return true;}}
    public bool GreekCapitalsInGsm{get{return true;}}
    public void Forget(){sessionKey=null;}

    // The session key lasts 2 hours and is renewed by every call; a fresh one is requested after 100 minutes.
    string Session(SmsSettings s){
        if(sessionKey!=null&&sessionUser==s.Username&&DateTime.UtcNow-sessionTime<TimeSpan.FromMinutes(100)){sessionTime=DateTime.UtcNow;return sessionKey;}
        // POST keeps the password out of the URL; the documented GET form is the fallback.
        string credentials=SmsHttp.Form("username",s.Username,"password",s.Password);
        string reply=SmsHttp.Send("POST",Base+"auth.php",credentials,"application/x-www-form-urlencoded").Body;
        if(!reply.StartsWith("OK ",StringComparison.Ordinal))reply=SmsHttp.Send("GET",Base+"auth.php?"+credentials,null,null).Body;
        if(!reply.StartsWith("OK ",StringComparison.Ordinal))throw new SmsException("Λάθος όνομα χρήστη ή κωδικός smsbox.");
        sessionKey=reply.Substring(3).Trim();sessionUser=s.Username;sessionTime=DateTime.UtcNow;return sessionKey;
    }

    public string Balance(SmsSettings s){
        string reply=SmsHttp.Send("GET",Base+"getbalance.php?"+SmsHttp.Form("sesskey",Session(s)),null,null).Body;
        double value;
        if(!double.TryParse(reply,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value))throw new SmsException("Μη αναμενόμενη απάντηση από το smsbox.");
        return SmsHttp.Number(value)+" SMS";
    }

    public List<SmsSent> Send(SmsSettings s,IList<string> phones,string text){
        var json=new StringBuilder();
        json.Append("{\"username\":").Append(SmsHttp.Json(s.Username)).Append(",\"password\":").Append(SmsHttp.Json(s.Password))
            .Append(",\"from\":").Append(SmsHttp.Json(s.Sender)).Append(",\"encoding\":").Append(SmsHttp.Json(SmsText.NeedsUnicode(text)?"UCS2":"UTF8")).Append(",\"messages\":[");
        for(int i=0;i<phones.Count;i++){if(i>0)json.Append(',');json.Append("{\"to\":").Append(SmsHttp.Json(phones[i])).Append(",\"text\":").Append(SmsHttp.Json(text)).Append('}');}
        json.Append("]}");
        var reply=SmsHttp.Send("POST",Base+"sendsms_batch.php",json.ToString(),"application/json");
        if(reply.Code!=200)throw new SmsException(reply.Code==401?"Λάθος όνομα χρήστη ή κωδικός smsbox.":reply.Code==402?"Δεν υπάρχουν αρκετά διαθέσιμα SMS στον λογαριασμό smsbox.":
            reply.Code==403?"Ο λογαριασμός smsbox δεν είναι ενεργός ή χρειάζεται αλλαγή κωδικού.":reply.Code==400?"Το smsbox δεν δέχτηκε το μήνυμα (αριθμός ή κείμενο).":"Σφάλμα smsbox ("+reply.Code+"). Δοκιμάστε ξανά.");
        var sent=new List<SmsSent>();var rejected=new List<string>();
        foreach(string item in SmsHttp.Objects(reply.Body)){
            string status=SmsHttp.Field(item,"status"),phone=SmsHttp.Field(item,"phone"),id=SmsHttp.Field(item,"smsid");
            if(status=="")continue;
            if(status=="queued"&&id!=""){int n;int.TryParse(SmsHttp.Field(item,"parts"),out n);sent.Add(new SmsSent{Id=id,Phone=phone,Parts=Math.Max(1,n)});}
            else rejected.Add(phone+" ("+status+")");
        }
        if(sent.Count==0)throw new SmsException(rejected.Count>0?"Το smsbox απέρριψε το μήνυμα: "+String.Join(", ",rejected):"Μη αναμενόμενη απάντηση από το smsbox.");
        return sent;
    }

    // Delivery reports: "id status phone" per line; 1 delivered, 2 not delivered, 20 accepted, 4x/5x/60 rejected.
    public Dictionary<string,SmsState> Status(SmsSettings s,IList<string> ids){
        var result=new Dictionary<string,SmsState>();
        string reply=SmsHttp.Send("GET",Base+"getreports.php?"+SmsHttp.Form("sesskey",Session(s),"ids",String.Join(",",ids)),null,null).Body;
        foreach(string line in reply.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)){
            var parts=line.Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);if(parts.Length<2)continue;
            string code=parts[1];
            result[parts[0]]=code=="1"?SmsState.Delivered:code=="2"||code=="41"||code=="42"||code=="44"||code=="52"||code=="60"?SmsState.Failed:SmsState.Pending;
        }
        return result;
    }
}

// Yuboto Octapush OMNI API v1.10 (https://octapush.yuboto.com): JSON, "Authorization: Basic <base64 key>".
internal sealed class Yuboto : ISmsProvider {
    const string Base="https://services.yuboto.com/omni/v1/";
    public string Id{get{return "yuboto";}}
    public string Name{get{return "Yuboto (Octapush)";}}
    public string UserLabel{get{return null;}}
    public string SecretLabel{get{return "Κλειδί OMNI API";}}
    public string Help{get{return "Octapush → Developers → API Key.";}}
    public bool HasStatus{get{return true;}}
    public bool GreekCapitalsInGsm{get{return true;}}
    public void Forget(){}
    // The platform shows the key both plain (a GUID) and already Base64-encoded; both are accepted.
    static Dictionary<string,string> Auth(SmsSettings s){
        string key=s.Password.Trim();
        if(Regex.IsMatch(key,"^[0-9A-Fa-f]{8}-([0-9A-Fa-f]{4}-){3}[0-9A-Fa-f]{12}$"))key=Convert.ToBase64String(Encoding.UTF8.GetBytes(key));
        return new Dictionary<string,string>{{"Authorization","Basic "+key}};
    }
    static System.Xml.Linq.XElement Call(SmsSettings s,string method,string path,string json){
        var reply=SmsHttp.Send(method,Base+path,json,json==null?null:"application/json",Auth(s));
        if(reply.Code==401||reply.Code==403)throw new SmsException("Λάθος κλειδί API Yuboto.");
        var root=SmsHttp.Parse(reply.Body);
        if(root==null)throw new SmsException(reply.Code>=500?"Η Yuboto δεν είναι διαθέσιμη αυτή τη στιγμή. Δοκιμάστε ξανά.":"Μη αναμενόμενη απάντηση από τη Yuboto ("+reply.Code+").");
        string error=SmsHttp.Value(root,"ErrorCode");
        if(error!=""&&error!="0"){string message=SmsHttp.Value(root,"ErrorMessage");throw new SmsException("Yuboto: "+(message!=""?message:"σφάλμα "+error));}
        return root;
    }
    public string Balance(SmsSettings s){
        var root=Call(s,"GET","UserBalance",null);
        double value;double.TryParse(SmsHttp.Value(root,"currentBalance"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value);
        return SmsHttp.Number(value)+(SmsHttp.Value(root,"type").Equals("money",StringComparison.OrdinalIgnoreCase)?" €":" μονάδες");
    }
    public List<SmsSent> Send(SmsSettings s,IList<string> phones,string text){
        bool unicode=SmsText.NeedsUnicode(text,true);int parts=SmsText.Parts(text,true);
        // "dlr" asks Yuboto to push reports to a callback URL (we have none, and the API wants one when it is true);
        // the status is read separately through the Dlr method. To confirm with a live key.
        var json=new StringBuilder("{\"dlr\":false,\"contacts\":[");
        for(int i=0;i<phones.Count;i++){if(i>0)json.Append(',');json.Append("{\"phonenumber\":").Append(SmsHttp.Json(phones[i])).Append('}');}
        json.Append("],\"sms\":{\"sender\":").Append(SmsHttp.Json(s.Sender)).Append(",\"text\":").Append(SmsHttp.Json(text))
            .Append(",\"typesms\":").Append(SmsHttp.Json(unicode?"Unicode":"sms")).Append(",\"longsms\":").Append(parts>1?"true":"false").Append(",\"validity\":1440}}");
        var root=Call(s,"POST","Send",json.ToString());
        var sent=new List<SmsSent>();var rejected=new List<string>();
        foreach(var m in SmsHttp.Items(root,"Message")){
            string id=SmsHttp.Value(m,"id"),phone=SmsHttp.Value(m,"phonenumber"),code=SmsHttp.Value(m,"errorCode");
            if(id!=""&&(code==""||code=="0"))sent.Add(new SmsSent{Id=id,Phone=phone,Parts=parts});else rejected.Add(phone+" ("+SmsHttp.Value(m,"status")+")");
        }
        if(sent.Count==0)throw new SmsException(rejected.Count>0?"Η Yuboto απέρριψε το μήνυμα: "+String.Join(", ",rejected):"Μη αναμενόμενη απάντηση από τη Yuboto.");
        return sent;
    }
    // DLR per message id; final SMS states per the OMNI documentation (2.2 Status of Messages).
    public Dictionary<string,SmsState> Status(SmsSettings s,IList<string> ids){
        var result=new Dictionary<string,SmsState>();
        foreach(string id in ids){
            var root=Call(s,"GET","Dlr?"+SmsHttp.Form("id",id),null);
            var sms=SmsHttp.Items(root,"dlr").FirstOrDefault(d=>SmsHttp.Value(d,"channel").Equals("sms",StringComparison.OrdinalIgnoreCase));
            string status=sms==null?SmsHttp.Value(root,"status"):SmsHttp.Value(sms,"status");
            result[id]=new[]{"Delivered","Seen","Clicked"}.Contains(status,StringComparer.OrdinalIgnoreCase)?SmsState.Delivered:
                new[]{"Not Delivered","Unknown","Error","Expired","Failed","Rejected","Canceled","Blacklisted","Unsubscribed"}.Contains(status,StringComparer.OrdinalIgnoreCase)?SmsState.Failed:SmsState.Pending;
        }
        return result;
    }
}

// modulus Messaging Platform native API (https://messaging.modulus.gr/ott-api/swagger): Bearer key.
// The API has no balance or status query; delivery reports are only pushed to a dlrUrl.
internal sealed class ModulusMessaging : ISmsProvider {
    const string Base="https://messaging.modulus.gr/ott-api/";
    public string Id{get{return "modulus";}}
    public string Name{get{return "modulus (mMP)";}}
    public string UserLabel{get{return null;}}
    public string SecretLabel{get{return "Κλειδί API";}}
    public string Help{get{return "modulus Messaging Platform → API key. Υπόλοιπο και παράδοση φαίνονται στην πλατφόρμα.";}}
    public bool HasStatus{get{return false;}}
    public bool GreekCapitalsInGsm{get{return false;}}
    public void Forget(){}
    // Checks the key with a harmless read; only 401 means a wrong key.
    public string Balance(SmsSettings s){
        var reply=SmsHttp.Send("GET",Base+"viber/templates/"+Uri.EscapeDataString(s.Sender==""?"nCall":s.Sender),null,null,SmsHttp.Bearer(s.Password.Trim()));
        if(reply.Code==401)throw new SmsException("Λάθος κλειδί API modulus.");
        if(reply.Code==429)throw new SmsException("Πολλές αποτυχημένες προσπάθειες· δοκιμάστε ξανά σε λίγα λεπτά.");
        if(reply.Code>=500)throw new SmsException("Η πλατφόρμα της modulus δεν είναι διαθέσιμη αυτή τη στιγμή.");
        return "το κλειδί είναι σωστό (το υπόλοιπο φαίνεται στην πλατφόρμα της modulus)";
    }
    public List<SmsSent> Send(SmsSettings s,IList<string> phones,string text){
        var json=new StringBuilder("{\"recipients\":[");
        for(int i=0;i<phones.Count;i++){if(i>0)json.Append(',');json.Append("{\"msisdn\":").Append(SmsHttp.Json(phones[i])).Append('}');}
        json.Append("],\"sms\":{\"sender\":").Append(SmsHttp.Json(s.Sender)).Append(",\"text\":").Append(SmsHttp.Json(text))
            .Append(",\"encoding\":").Append(SmsHttp.Json(SmsText.NeedsUnicode(text,false)?"ucs2":"gsm")).Append("}}");
        var reply=SmsHttp.Send("POST",Base+"message",json.ToString(),"application/json",SmsHttp.Bearer(s.Password.Trim()));
        var root=SmsHttp.Parse(reply.Body);
        if(reply.Code!=200){string detail=SmsHttp.Value(root,"message");
            throw new SmsException(reply.Code==401?"Λάθος κλειδί API modulus.":reply.Code==403?"Ο λογαριασμός modulus δεν έχει πρόσβαση στο API.":
                reply.Code==429?"Πολλές αποτυχημένες προσπάθειες· δοκιμάστε ξανά σε λίγα λεπτά.":"modulus: "+(detail!=""?detail:"σφάλμα "+reply.Code));}
        int parts=SmsText.Parts(text,false);var sent=new List<SmsSent>();var rejected=new List<string>();
        foreach(var m in SmsHttp.Items(root,"messages")){
            string id=SmsHttp.Value(m,"messageId"),phone=SmsHttp.Value(m,"recipient","msisdn"),code=SmsHttp.Value(m,"status","code");
            if(id!=""&&code=="10")sent.Add(new SmsSent{Id=id,Phone=phone,Parts=parts});else rejected.Add(phone+" ("+SmsHttp.Value(m,"status","message")+")");
        }
        if(sent.Count==0)throw new SmsException(rejected.Count>0?"Η modulus απέρριψε το μήνυμα: "+String.Join(", ",rejected):"Μη αναμενόμενη απάντηση από τη modulus.");
        return sent;
    }
    public Dictionary<string,SmsState> Status(SmsSettings s,IList<string> ids){return new Dictionary<string,SmsState>();}
}

// SMS.to REST API (https://developers.sms.to): Bearer API key; encoding is chosen by the platform.
internal sealed class SmsTo : ISmsProvider {
    public string Id{get{return "smsto";}}
    public string Name{get{return "SMS.to";}}
    public string UserLabel{get{return null;}}
    public string SecretLabel{get{return "Κλειδί API";}}
    public string Help{get{return "SMS.to → API Keys.";}}
    public bool HasStatus{get{return true;}}
    public bool GreekCapitalsInGsm{get{return false;}}
    public void Forget(){}
    static System.Xml.Linq.XElement Call(SmsSettings s,string method,string url,string json){
        var reply=SmsHttp.Send(method,url,json,json==null?null:"application/json",SmsHttp.Bearer(s.Password.Trim()));
        if(reply.Code==401||reply.Code==403)throw new SmsException("Λάθος κλειδί API SMS.to.");
        var root=SmsHttp.Parse(reply.Body);
        if(reply.Code>=400||root==null){string detail=SmsHttp.Value(root,"message");throw new SmsException("SMS.to: "+(detail!=""?detail:"σφάλμα "+reply.Code));}
        return root;
    }
    public string Balance(SmsSettings s){
        var root=Call(s,"GET","https://auth.sms.to/api/balance",null);
        double value;double.TryParse(SmsHttp.Value(root,"balance"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value);
        return SmsHttp.Number(value)+" "+SmsHttp.Value(root,"currency");
    }
    public List<SmsSent> Send(SmsSettings s,IList<string> phones,string text){
        int parts=SmsText.Parts(text,false);var sent=new List<SmsSent>();
        foreach(string phone in phones){
            string json="{\"message\":"+SmsHttp.Json(text)+",\"to\":"+SmsHttp.Json("+"+phone)+",\"sender_id\":"+SmsHttp.Json(s.Sender)+"}";
            var root=Call(s,"POST","https://api.sms.to/sms/send",json);
            string id=SmsHttp.Value(root,"message_id");
            if(SmsHttp.Value(root,"success")!="true"||id==""){string detail=SmsHttp.Value(root,"message");throw new SmsException("SMS.to: "+(detail!=""?detail:"το μήνυμα δεν έγινε δεκτό"));}
            sent.Add(new SmsSent{Id=id,Phone=phone,Parts=parts});
        }
        return sent;
    }
    public Dictionary<string,SmsState> Status(SmsSettings s,IList<string> ids){
        var result=new Dictionary<string,SmsState>();
        foreach(string id in ids){
            string status=SmsHttp.Value(Call(s,"GET","https://api.sms.to/message/"+Uri.EscapeDataString(id),null),"status").ToUpperInvariant();
            result[id]=status=="DELIVERED"?SmsState.Delivered:status=="FAILED"||status=="REJECTED"||status=="EXPIRED"||status=="UNDELIVERED"?SmsState.Failed:SmsState.Pending;
        }
        return result;
    }
}

// Text rules shared by the providers: Greek mobile numbers and SMS length.
internal static class SmsText {
    const string Doubled="[]{}~^|\\€";
    // Plain ASCII fits the GSM alphabet; providers that convert Greek to capitals also send those in GSM.
    // Lowercase / accented Greek and anything else needs Unicode (70 characters per SMS).
    internal static bool NeedsUnicode(string text,bool greekCapitals=true){return (text??"").Any(c=>c>127&&c!='€'&&!(greekCapitals&&c>='Α'&&c<='Ω'&&c!='\u03A2'));}
    internal static int Length(string text,bool greekCapitals=true){return NeedsUnicode(text,greekCapitals)?text.Length:text.Length+text.Count(c=>Doubled.IndexOf(c)>=0);}
    internal static int Parts(string text,bool greekCapitals=true){
        int length=Length(text,greekCapitals);if(length==0)return 0;
        if(NeedsUnicode(text,greekCapitals))return length<=70?1:(length+66)/67;
        return length<=160?1:(length+152)/153;
    }
    internal static int MaxLength(string text,bool greekCapitals=true){return NeedsUnicode(text,greekCapitals)?268:612;}
    // 69xxxxxxxx, 3069xxxxxxxx, +3069xxxxxxxx, 003069xxxxxxxx -> 3069xxxxxxxx; anything else -> null.
    internal static string Mobile(string number){
        string digits=new string((number??"").Where(char.IsDigit).ToArray());
        if(digits.StartsWith("00"))digits=digits.Substring(2);
        if(digits.Length==10&&digits.StartsWith("69"))digits="30"+digits;
        return digits.Length==12&&digits.StartsWith("3069")?digits:null;
    }
    // 306912345678 -> 691 234 5678
    internal static string Display(string phone){
        string p=phone??"";if(p.Length==12&&p.StartsWith("30"))p=p.Substring(2);
        return p.Length==10?p.Substring(0,3)+" "+p.Substring(3,3)+" "+p.Substring(6):p;
    }
}

// Sent messages, newest last, kept per Windows user (last 2000).
internal sealed class SmsRecord {
    internal DateTime Time;
    internal string Provider="",Phone="",Name="",Id="",Text="";
    internal int Parts;
    internal SmsState State;
}

internal static class SmsHistory {
    static readonly object gate=new object();
    static string FilePath{get{return SmsSettings.FileIn("sms-history.txt");}}
    static string Escape(string s){return (s??"").Replace("\\","\\\\").Replace("\t","\\t").Replace("\r","").Replace("\n","\\n");}
    static string Unescape(string s){var b=new StringBuilder();for(int i=0;i<s.Length;i++){if(s[i]=='\\'&&i+1<s.Length){char n=s[++i];b.Append(n=='t'?'\t':n=='n'?'\n':n);}else b.Append(s[i]);}return b.ToString();}
    internal static List<SmsRecord> Load(){
        lock(gate){
            var list=new List<SmsRecord>();
            try{
                if(!File.Exists(FilePath))return list;
                foreach(string line in File.ReadAllLines(FilePath,Encoding.UTF8)){
                    var f=line.Split('\t');if(f.Length<8)continue;DateTime time;int parts;SmsState state;
                    if(!DateTime.TryParse(f[0],System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out time))continue;
                    int.TryParse(f[4],out parts);Enum.TryParse(f[6],out state);
                    list.Add(new SmsRecord{Time=time,Provider=f[1],Phone=f[2],Name=Unescape(f[3]),Parts=parts,Id=f[5],State=state,Text=Unescape(f[7])});
                }
            }catch(Exception e){Host.Error(e);}
            return list;
        }
    }
    static void Save(List<SmsRecord> list){
        if(list.Count>2000)list=list.Skip(list.Count-2000).ToList();
        Directory.CreateDirectory(SmsSettings.Folder);
        File.WriteAllLines(FilePath,list.Select(r=>String.Join("\t",r.Time.ToString("o"),r.Provider,r.Phone,Escape(r.Name),r.Parts.ToString(),r.Id,r.State.ToString(),Escape(r.Text))),Encoding.UTF8);
    }
    internal static void Add(IEnumerable<SmsRecord> records){lock(gate){try{var list=Load();list.AddRange(records);Save(list);}catch(Exception e){Host.Error(e);}}}
    internal static void Update(string provider,Dictionary<string,SmsState> states){
        if(states.Count==0)return;
        lock(gate){try{var list=Load();bool changed=false;
            foreach(var r in list)if(r.Provider==provider&&states.ContainsKey(r.Id)&&r.State!=states[r.Id]){r.State=states[r.Id];changed=true;}
            if(changed)Save(list);}catch(Exception e){Host.Error(e);}}
    }
    internal static void Clear(){lock(gate){try{if(File.Exists(FilePath))File.Delete(FilePath);}catch(Exception e){Host.Error(e);}}}
}

// Windows: SMS settings, send window and history.
internal static class Sms {
    static void Run<T>(Func<T> work,Action<T> done,Action<string> failed,string provider){
        var ui=Dispatcher.CurrentDispatcher;
        System.Threading.ThreadPool.QueueUserWorkItem(_=>{
            T result=default(T);string error=null;
            try{result=work();}
            catch(SmsException e){error=e.Message;}
            catch(WebException){error=SmsHttp.Unreachable(provider);}
            catch(Exception e){Host.Error(e);error="Σφάλμα κατά την επικοινωνία με "+provider+".";}
            ui.BeginInvoke(new Action(()=>{try{if(error==null)done(result);else failed(error);}catch(Exception e){Host.Error(e);}}));
        });
    }
    static TextBlock Muted(string text,double size=12){var t=Surface.Label(text,size);t.SetResourceReference(TextBlock.ForegroundProperty,"Muted");return t;}
    static TextBlock Caption(string text){var t=Muted(text);t.Margin=new Thickness(0,6,0,3);return t;}
    static TextBlock Heading(string text){var t=Surface.Label(text,19);t.FontWeight=FontWeights.SemiBold;return t;}

    internal static void Settings(IntPtr owner){
        var s=SmsSettings.Load();
        var panel=new StackPanel{Margin=new Thickness(24)};
        panel.Children.Add(Heading("Ρυθμίσεις SMS"));
        panel.Children.Add(Muted("Τα SMS στέλνονται από τον δικό σας λογαριασμό στον πάροχο και χρεώνονται εκεί."));
        panel.Children.Add(Caption("Πάροχος"));
        var provider=new ComboBox();foreach(var p in SmsProviders.All)provider.Items.Add(p.Name);
        provider.SelectedIndex=Math.Max(0,Array.FindIndex(SmsProviders.All,p=>p.Id==s.Provider));panel.Children.Add(provider);
        var help=Muted("",12);help.Margin=new Thickness(0,4,0,0);panel.Children.Add(help);
        var userCaption=Caption("");panel.Children.Add(userCaption);var user=new TextBox{Text=s.Username};panel.Children.Add(user);
        var secretCaption=Caption("");panel.Children.Add(secretCaption);var password=new PasswordBox{Password=s.Password};panel.Children.Add(password);
        panel.Children.Add(Caption("Αποστολέας (έως 11 λατινικοί χαρακτήρες ή 13 ψηφία)"));var sender=new TextBox{Text=s.Sender,MaxLength=13};panel.Children.Add(sender);
        var result=Muted("",12);result.Margin=new Thickness(0,10,0,0);panel.Children.Add(result);
        Func<ISmsProvider> chosen=()=>SmsProviders.All[Math.Max(0,provider.SelectedIndex)];
        Action labels=()=>{var p=chosen();help.Text=p.Help;userCaption.Text=p.UserLabel??"";
            userCaption.Visibility=user.Visibility=p.UserLabel==null?Visibility.Collapsed:Visibility.Visible;secretCaption.Text=p.SecretLabel;result.Text="";};
        provider.SelectionChanged+=(x,y)=>labels();labels();
        Func<SmsSettings> read=()=>{var p=chosen();return new SmsSettings{Provider=p.Id,Username=p.UserLabel==null?"":user.Text.Trim(),Password=password.Password.Trim(),Sender=sender.Text.Trim()};};
        Func<string> validate=()=>{var x=read();var p=chosen();
            if((p.UserLabel!=null&&x.Username=="")||x.Password=="")return "Συμπληρώστε τα στοιχεία σύνδεσης.";
            if(!Regex.IsMatch(x.Sender,"^([A-Za-z0-9 !()*#%.,?\\-=+;&]{1,11}|[0-9]{1,13})$"))return "Ο αποστολέας πρέπει να έχει έως 11 λατινικούς χαρακτήρες ή έως 13 ψηφία.";
            return null;};
        var w=Host.Popup("Ρυθμίσεις SMS",panel,owner);
        var actions=new UniformGrid{Columns=3,Margin=new Thickness(0,14,0,0)};
        Button test=null;
        test=Surface.Button("Έλεγχος",()=>{
            var x=read();var p=chosen();if((p.UserLabel!=null&&x.Username=="")||x.Password==""){result.Text="Συμπληρώστε τα στοιχεία σύνδεσης.";return;}
            test.IsEnabled=false;result.Text="Σύνδεση…";p.Forget();
            Run(()=>p.Balance(x),balance=>{test.IsEnabled=true;result.Text="Συνδέθηκε. Υπόλοιπο: "+balance;},error=>{test.IsEnabled=true;result.Text=error;},p.Name);
        });test.Margin=new Thickness(0,0,4,0);actions.Children.Add(test);
        var save=Surface.Button("Αποθήκευση",()=>{string problem=validate();if(problem!=null){result.Text=problem;return;}
            try{read().Save();chosen().Forget();w.Close();}catch(Exception e){Host.Error(e);result.Text="Δεν ήταν δυνατή η αποθήκευση των ρυθμίσεων.";}},true);
        save.Margin=new Thickness(4,0,4,0);save.IsDefault=true;actions.Children.Add(save);
        var cancel=Surface.Button("Ακύρωση",()=>w.Close());cancel.Margin=new Thickness(4,0,0,0);cancel.IsCancel=true;actions.Children.Add(cancel);
        panel.Children.Add(actions);
        w.ShowDialog();
    }

    internal static void Compose(IntPtr owner,string number,string name){
        var s=SmsSettings.Load();
        if(!s.Complete){Settings(owner);s=SmsSettings.Load();if(!s.Complete)return;}
        var provider=SmsProviders.Find(s.Provider);
        var panel=new StackPanel{Margin=new Thickness(24)};
        panel.Children.Add(Heading("Αποστολή SMS"));
        panel.Children.Add(Muted("Αποστολέας: "+s.Sender+" · μέσω "+provider.Name));
        panel.Children.Add(Caption(String.IsNullOrEmpty(name)?"Προς (κινητό)":"Προς: "+name));
        var to=new TextBox{Text=number??""};panel.Children.Add(to);
        panel.Children.Add(Caption("Μήνυμα"));
        var text=new TextBox{AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=110,MaxHeight=220,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};panel.Children.Add(text);
        var counter=Muted("",12);counter.Margin=new Thickness(0,4,0,0);counter.TextAlignment=TextAlignment.Right;panel.Children.Add(counter);
        var result=Surface.Label("",13);result.Margin=new Thickness(0,8,0,0);panel.Children.Add(result);
        var w=Host.Popup("Αποστολή SMS",panel,owner);
        var actions=new UniformGrid{Columns=2,Margin=new Thickness(0,12,0,0)};
        Button send=null;bool sending=false,sentOnce=false;
        bool caps=provider.GreekCapitalsInGsm;
        Action update=()=>{
            int length=SmsText.Length(text.Text,caps),parts=SmsText.Parts(text.Text,caps),max=SmsText.MaxLength(text.Text,caps);bool unicode=SmsText.NeedsUnicode(text.Text,caps);
            counter.Text=length+" / "+max+" χαρακτήρες · "+parts+" SMS"+(unicode?" · Unicode (70 ανά SMS)":"");
            send.IsEnabled=!sending&&length>0&&length<=max&&to.Text.Trim()!="";
        };
        send=Surface.Button("Αποστολή",()=>{
            string phone=SmsText.Mobile(to.Text);
            if(phone==null){result.Text="Ο αριθμός πρέπει να είναι ελληνικό κινητό (69…).";return;}
            if(sentOnce&&MessageBox.Show(w,"Το μήνυμα έχει ήδη σταλεί. Να σταλεί ξανά;","Αποστολή SMS",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            sending=true;update();result.Text="Αποστολή…";string body=text.Text;
            Run(()=>provider.Send(s,new[]{phone},body),sent=>{
                sending=false;sentOnce=true;update();int parts=sent.Sum(x=>x.Parts);
                SmsHistory.Add(sent.Select(x=>new SmsRecord{Time=DateTime.Now,Provider=provider.Id,Phone=x.Phone==""?phone:x.Phone,Name=name??"",Id=x.Id,Parts=x.Parts,Text=body,State=SmsState.Pending}));
                if(!provider.HasStatus){result.Text="Στάλθηκε ("+parts+" SMS).";return;}
                result.Text="Στάλθηκε ("+parts+" SMS). Αναμονή επιβεβαίωσης παράδοσης…";
                Track(w,s,provider,sent.Select(x=>x.Id).ToList(),result,parts);
            },error=>{sending=false;update();result.Text=error;},provider.Name);
        },true);
        send.Margin=new Thickness(0,0,4,0);actions.Children.Add(send);
        var close=Surface.Button("Κλείσιμο",()=>w.Close());close.Margin=new Thickness(4,0,0,0);close.IsCancel=true;actions.Children.Add(close);
        panel.Children.Add(actions);
        text.TextChanged+=(x,y)=>update();to.TextChanged+=(x,y)=>update();update();
        w.Loaded+=(x,y)=>(to.Text.Trim()==""?(Control)to:text).Focus();
        w.ShowDialog();
    }

    // Delivery status while the window is open: checked after 5, 15, 30, 60 and 120 seconds.
    static void Track(Window w,SmsSettings s,ISmsProvider provider,List<string> ids,TextBlock result,int parts){
        int[] delays={5,10,15,30,60};int step=0;bool open=true;w.Closed+=(x,y)=>open=false;
        var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(delays[0])};
        Action next=()=>{if(open&&++step<delays.Length){timer.Interval=TimeSpan.FromSeconds(delays[step]);timer.Start();}
            else if(open)result.Text="Στάλθηκε ("+parts+" SMS). Η παράδοση δεν έχει επιβεβαιωθεί ακόμη· δείτε το «Ιστορικό SMS».";};
        timer.Tick+=(x,y)=>{
            timer.Stop();if(!open)return;
            Run(()=>provider.Status(s,ids),status=>{
                SmsHistory.Update(provider.Id,status);if(!open)return;
                SmsState state;if(!status.TryGetValue(ids[0],out state))state=SmsState.Pending;
                if(state==SmsState.Delivered)result.Text="\u2713 Παραδόθηκε ("+parts+" SMS).";
                else if(state==SmsState.Failed)result.Text="Το μήνυμα δεν παραδόθηκε.";
                else next();
            },error=>next(),provider.Name);
        };
        timer.Start();
    }

    static string StateText(SmsRecord r){return r.State==SmsState.Delivered?"\u2713 Παραδόθηκε":r.State==SmsState.Failed?"\u2717 Δεν παραδόθηκε":SmsProviders.Find(r.Provider).HasStatus?"Σε αναμονή":"Στάλθηκε";}

    internal static void History(IntPtr owner){
        var panel=new DockPanel{Margin=new Thickness(24)};
        var heading=Heading("Ιστορικό SMS");DockPanel.SetDock(heading,Dock.Top);panel.Children.Add(heading);
        var info=Muted("",12);DockPanel.SetDock(info,Dock.Top);panel.Children.Add(info);
        var actions=new UniformGrid{Columns=3,Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(actions,Dock.Bottom);panel.Children.Add(actions);
        var list=new ListBox{Height=380,HorizontalContentAlignment=HorizontalAlignment.Stretch};panel.Children.Add(list);
        var w=Host.Popup("Ιστορικό SMS",panel,owner);w.Width=520;
        List<SmsRecord> records=null;
        Action fill=()=>{
            records=SmsHistory.Load();records.Reverse();list.Items.Clear();
            foreach(var r in records){
                var item=new StackPanel{Margin=new Thickness(2,2,2,4)};
                var top=new DockPanel();
                var state=Muted(StateText(r),12);state.Margin=new Thickness(12,0,0,0);DockPanel.SetDock(state,Dock.Right);
                if(r.State==SmsState.Delivered)state.SetResourceReference(TextBlock.ForegroundProperty,"Primary");
                top.Children.Add(state);
                var who=Surface.Label((r.Name!=""?r.Name+" · ":"")+SmsText.Display(r.Phone),13);who.FontWeight=FontWeights.SemiBold;who.Margin=new Thickness(0);top.Children.Add(who);
                item.Children.Add(top);
                var meta=Muted(r.Time.ToString("dd/MM/yyyy HH:mm")+" · "+r.Parts+" SMS · "+SmsProviders.Find(r.Provider).Name,11);meta.Margin=new Thickness(0,1,0,2);item.Children.Add(meta);
                var body=Surface.Label(r.Text.Length>140?r.Text.Substring(0,140)+"…":r.Text,12);body.Margin=new Thickness(0);item.Children.Add(body);
                list.Items.Add(item);
            }
            info.Text=records.Count==0?"Δεν έχουν σταλεί SMS από αυτόν τον υπολογιστή.":records.Count+" μηνύματα (τα πιο πρόσφατα πρώτα).";
        };
        Button refresh=null;
        refresh=Surface.Button("Ανανέωση",()=>{
            var s=SmsSettings.Load();var pending=SmsHistory.Load().Where(r=>r.State==SmsState.Pending&&r.Provider==s.Provider&&r.Time>DateTime.Now.AddDays(-30)).Select(r=>r.Id).Distinct().Take(100).ToList();
            var provider=SmsProviders.Find(s.Provider);
            if(pending.Count==0||!s.Complete||!provider.HasStatus){info.Text=provider.HasStatus?"Δεν υπάρχουν μηνύματα σε αναμονή.":provider.Name+": η κατάσταση παράδοσης φαίνεται μόνο στην πλατφόρμα του παρόχου.";return;}
            refresh.IsEnabled=false;info.Text="Έλεγχος…";
            Run(()=>provider.Status(s,pending),status=>{refresh.IsEnabled=true;SmsHistory.Update(provider.Id,status);fill();},error=>{refresh.IsEnabled=true;info.Text=error;},provider.Name);
        });refresh.Margin=new Thickness(0,0,4,0);actions.Children.Add(refresh);
        var again=Surface.Button("Νέο SMS",()=>{int i=list.SelectedIndex;var r=i>=0&&records!=null&&i<records.Count?records[i]:null;
            Compose(new System.Windows.Interop.WindowInteropHelper(w).Handle,r==null?"":SmsText.Display(r.Phone),r==null?"":r.Name);fill();},true);
        again.Margin=new Thickness(4,0,4,0);actions.Children.Add(again);
        var close=Surface.Button("Κλείσιμο",()=>w.Close());close.Margin=new Thickness(4,0,0,0);close.IsCancel=true;actions.Children.Add(close);
        var menu=new ContextMenu();menu.Resources.MergedDictionaries.Add(Host.Theme);
        var clear=new MenuItem{Header="Διαγραφή ιστορικού SMS…"};
        clear.Click+=(x,y)=>{if(MessageBox.Show(w,"Να διαγραφεί όλο το ιστορικό SMS από αυτόν τον υπολογιστή; Τα μηνύματα δεν επηρεάζονται.","Ιστορικό SMS",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes){SmsHistory.Clear();fill();}};
        menu.Items.Add(clear);list.ContextMenu=menu;
        list.MouseDoubleClick+=(x,y)=>{if(list.SelectedIndex>=0)again.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));};
        fill();
        w.ShowDialog();
    }
}
}
