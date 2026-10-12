// Copyright (C) 2026 netmind. GPL-3.0-or-later.
// Modern presentation of the existing in-process SIP controls. No network or IPC.
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Netmind.UI {
internal static class Native {
    internal delegate bool EnumProc(IntPtr h,IntPtr p);
    [DllImport("user32.dll")] internal static extern bool EnumChildWindows(IntPtr h,EnumProc callback,IntPtr p);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h,StringBuilder text,int count);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] internal static extern IntPtr SetFocus(IntPtr h);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr h,int command);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr h,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,StringBuilder text,int count);
    [DllImport("user32.dll")] internal static extern int GetDlgCtrlID(IntPtr h);
    [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr h);
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] internal static extern bool IsWindowEnabled(IntPtr h);
    [DllImport("user32.dll")] internal static extern int GetWindowLong(IntPtr h,int index);
    [DllImport("user32.dll")] internal static extern int SetWindowLong(IntPtr h,int index,int value);
    [DllImport("user32.dll")] internal static extern bool SetLayeredWindowAttributes(IntPtr h,uint key,byte alpha,uint flags);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr h,out RECT rect);
    [DllImport("user32.dll")] internal static extern bool GetClientRect(IntPtr h,out RECT rect);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    [DllImport("kernel32.dll")] static extern uint GetCurrentProcessId();
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern IntPtr SendMessage(IntPtr h,int message,IntPtr w,IntPtr l);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="SendMessageW")] internal static extern IntPtr SendText(IntPtr h,int message,IntPtr w,string text);
    [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr h,int message,IntPtr w,IntPtr l);
    [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(IntPtr h,int attr,ref int value,int size);
    [DllImport("dwmapi.dll")] internal static extern int DwmFlush();
    internal delegate IntPtr SubclassProc(IntPtr h,uint message,IntPtr w,IntPtr l,UIntPtr id,UIntPtr data);
    static readonly SubclassProc PaintGuard=GuardPaint;
    static readonly UIntPtr PaintGuardId=new UIntPtr(0x4e4d);
    [DllImport("comctl32.dll")] static extern bool SetWindowSubclass(IntPtr h,SubclassProc callback,UIntPtr id,UIntPtr data);
    [DllImport("comctl32.dll")] static extern bool RemoveWindowSubclass(IntPtr h,SubclassProc callback,UIntPtr id);
    [DllImport("comctl32.dll")] static extern IntPtr DefSubclassProc(IntPtr h,uint message,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] static extern bool ValidateRect(IntPtr h,IntPtr rect);
    [DllImport("user32.dll")] static extern IntPtr BeginPaint(IntPtr h,out PAINTSTRUCT paint);
    [DllImport("user32.dll")] static extern bool EndPaint(IntPtr h,ref PAINTSTRUCT paint);
    [DllImport("user32.dll")] static extern int FillRect(IntPtr dc,ref RECT rect,IntPtr brush);
    [DllImport("gdi32.dll")] static extern IntPtr CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);
    [StructLayout(LayoutKind.Sequential)] struct PAINTSTRUCT {public IntPtr dc;public int erase;public RECT rect;public int restore,update;[MarshalAs(UnmanagedType.ByValArray,SizeConst=32)]public byte[] reserved;}
    static IntPtr GuardPaint(IntPtr h,uint message,IntPtr w,IntPtr l,UIntPtr id,UIntPtr data){
        // Backend controls retain visibility and state, but never paint legacy UI.
        // WPF receives the input and renders the complete product surface.
        if(message==0xf){if(data.ToUInt32()==1){PAINTSTRUCT paint;var dc=BeginPaint(h,out paint);RECT rect;GetClientRect(h,out rect);var canvas=Host.Canvas;uint color=(uint)(canvas.R|(canvas.G<<8)|(canvas.B<<16));var brush=CreateSolidBrush(color);FillRect(dc,ref rect,brush);DeleteObject(brush);EndPaint(h,ref paint);}else ValidateRect(h,IntPtr.Zero);return IntPtr.Zero;}
        if(message==0x14)return new IntPtr(1);
        if((message==0x85&&data.ToUInt32()==0)||message==0x317||message==0x318)return IntPtr.Zero;
        if(message==0x82)RemoveWindowSubclass(h,PaintGuard,PaintGuardId);
        return DefSubclassProc(h,message,w,l);
    }
    internal static void GuardBackendPainting(IntPtr parent,IntPtr host){SetWindowSubclass(parent,PaintGuard,PaintGuardId,new UIntPtr(1));EnumChildWindows(parent,(h,p)=>{if(h!=host&&!Class(h).StartsWith("HwndWrapper",StringComparison.Ordinal))SetWindowSubclass(h,PaintGuard,PaintGuardId,UIntPtr.Zero);return true;},IntPtr.Zero);}
    [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct NMHDR {public IntPtr hwnd;public UIntPtr id;public int code;}
    [StructLayout(LayoutKind.Sequential)] internal struct NMUPDOWN {public NMHDR header;public int position,delta;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] internal struct LVITEM {public uint mask;public int item,subItem;public uint state,stateMask;public IntPtr text;public int textLength,image;public IntPtr param;public int indent,groupId;public uint columns;public IntPtr columnIndices,columnFormats;public int group;}
    internal static string Text(IntPtr h) {int n=GetWindowTextLength(h);if(n<=0)return "";var s=new StringBuilder(n+1);GetWindowText(h,s,s.Capacity);return s.ToString();}
    internal static string Class(IntPtr h) {var s=new StringBuilder(128);GetClassName(h,s,s.Capacity);return s.ToString();}
    internal static bool Own(IntPtr h) {uint p;GetWindowThreadProcessId(h,out p);return p==GetCurrentProcessId() && IsWindow(h);}
    internal static int Value(IntPtr h,int m,int w=0) {return SendMessage(h,m,new IntPtr(w),IntPtr.Zero).ToInt32();}
    internal static void Command(IntPtr h,int id) {if(Own(h))PostMessage(h,0x111,new IntPtr(id),IntPtr.Zero);}
    internal static void Notify(IntPtr h,int code) {
        var n=new NMHDR{hwnd=h,id=new UIntPtr((uint)GetDlgCtrlID(h)),code=code};
        var p=Marshal.AllocHGlobal(Marshal.SizeOf(n));try{Marshal.StructureToPtr(n,p,false);SendMessage(GetParent(h),0x4e,new IntPtr(GetDlgCtrlID(h)),p);}finally{Marshal.FreeHGlobal(p);}
    }
    internal static void Spin(IntPtr h,int delta) {
        var n=new NMUPDOWN{header=new NMHDR{hwnd=h,id=new UIntPtr((uint)GetDlgCtrlID(h)),code=-722},delta=delta};
        var p=Marshal.AllocHGlobal(Marshal.SizeOf(n));try{Marshal.StructureToPtr(n,p,false);SendMessage(GetParent(h),0x4e,new IntPtr(GetDlgCtrlID(h)),p);}finally{Marshal.FreeHGlobal(p);}
    }
    internal static void Changed(IntPtr h,int code) {SendMessage(GetParent(h),0x111,new IntPtr(GetDlgCtrlID(h)|(code<<16)),h);}
    internal static string Item(IntPtr h,int m,int index) {
        int len=Value(h,m==0x148?0x149:0x18a,index);if(len<0 || len>32767)return "";
        var p=Marshal.AllocHGlobal((len+1)*2);try{SendMessage(h,m,new IntPtr(index),p);return Marshal.PtrToStringUni(p)??"";}finally{Marshal.FreeHGlobal(p);}
    }
    internal static string ListCell(IntPtr h,int row,int col) {
        var text=Marshal.AllocHGlobal(2048);var item=new LVITEM{item=row,subItem=col,text=text,textLength=1024};
        var p=Marshal.AllocHGlobal(Marshal.SizeOf(item));try{Marshal.StructureToPtr(item,p,false);SendMessage(h,0x1073,new IntPtr(row),p);return Marshal.PtrToStringUni(text)??"";}finally{Marshal.FreeHGlobal(p);Marshal.FreeHGlobal(text);}
    }
    internal static void ListSelect(IntPtr h,int row) {
        var item=new LVITEM{state=0,stateMask=3};var p=Marshal.AllocHGlobal(Marshal.SizeOf(item));
        try{Marshal.StructureToPtr(item,p,false);SendMessage(h,0x102b,new IntPtr(-1),p);item.state=3;Marshal.StructureToPtr(item,p,false);SendMessage(h,0x102b,new IntPtr(row),p);}finally{Marshal.FreeHGlobal(p);}
    }
    // Several selected rows (Ctrl/Shift click); the focused one is the row used by Call / Edit.
    internal static void ListSelectMany(IntPtr h,IEnumerable<int> rows,int focus) {
        var item=new LVITEM{state=0,stateMask=3};var p=Marshal.AllocHGlobal(Marshal.SizeOf(item));
        try{Marshal.StructureToPtr(item,p,false);SendMessage(h,0x102b,new IntPtr(-1),p);
            foreach(int row in rows){item.state=row==focus?3u:2u;Marshal.StructureToPtr(item,p,false);SendMessage(h,0x102b,new IntPtr(row),p);}}
        finally{Marshal.FreeHGlobal(p);}
    }
    [StructLayout(LayoutKind.Sequential)] struct TCITEM {public uint mask,state,stateMask;public IntPtr text;public int textMax,image;public IntPtr param;}
    internal static string TabText(IntPtr h,int index){var text=Marshal.AllocHGlobal(512);var item=new TCITEM{mask=1,text=text,textMax=255};var p=Marshal.AllocHGlobal(Marshal.SizeOf(item));try{Marshal.StructureToPtr(item,p,false);SendMessage(h,0x133c,new IntPtr(index),p);return Marshal.PtrToStringUni(text)??"";}finally{Marshal.FreeHGlobal(p);Marshal.FreeHGlobal(text);}}
    internal static void ListSelectAll(IntPtr h){var item=new LVITEM{state=2,stateMask=2};var p=Marshal.AllocHGlobal(Marshal.SizeOf(item));try{Marshal.StructureToPtr(item,p,false);SendMessage(h,0x102b,new IntPtr(-1),p);}finally{Marshal.FreeHGlobal(p);}}
    internal static void ListActivate(IntPtr h,int row){var p=Marshal.AllocHGlobal(128);try{for(int i=0;i<128;i++)Marshal.WriteByte(p,i,0);var n=new NMHDR{hwnd=h,id=new UIntPtr((uint)GetDlgCtrlID(h)),code=-3};Marshal.StructureToPtr(n,p,false);Marshal.WriteInt32(p,Marshal.SizeOf(n),row);SendMessage(GetParent(h),0x4e,new IntPtr(GetDlgCtrlID(h)),p);}finally{Marshal.FreeHGlobal(p);}}
}
internal class ControlInfo {
    internal IntPtr H;internal int Id,Style;internal string Class,Caption;internal Native.RECT Rect;
    internal bool Input {get{return Class=="Edit"||Class=="ComboBox"||Class=="ListBox"||Class=="msctls_trackbar32"||Class=="msctls_updown32"||Class.StartsWith("RichEdit",StringComparison.OrdinalIgnoreCase)||Class=="SysListView32";}}
}
public static class Host {
    // First initializer, before any WPF type is used: the process is per-monitor DPI aware
    // (custom.manifest), but WPF only rescales on DPI changes when this switch is off,
    // and a native host has no TargetFramework to turn it off automatically.
    static readonly bool perMonitorDpi=EnablePerMonitorDpi();
    static bool EnablePerMonitorDpi(){try{AppContext.SetSwitch("Switch.System.Windows.DoNotScaleForDpiChanges",false);return true;}catch{return false;}}
    static readonly Dictionary<IntPtr,Surface> surfaces=new Dictionary<IntPtr,Surface>();
    internal static readonly ResourceDictionary Theme=LoadTheme();
    static readonly Dictionary<string,string> Greek=LoadGreek();
    internal static string Mode=ReadMode();
    static Host(){
        // An exception in a WPF handler would cross the MFC message loop and end the process,
        // possibly during a call: log it and keep running.
        Dispatcher.CurrentDispatcher.UnhandledException+=(s,e)=>{Error(e.Exception);e.Handled=true;};
    }
    static ResourceDictionary LoadTheme(){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("Netmind.Theme.xaml"))return (ResourceDictionary)XamlReader.Load(s);}
    static Dictionary<string,string> LoadGreek(){var result=new Dictionary<string,string>(StringComparer.Ordinal);using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("Netmind.Greek.txt"))using(var reader=new StreamReader(s,Encoding.UTF8)){string line,key=null;while((line=reader.ReadLine())!=null){if(line.StartsWith("[")&&line.EndsWith("]")){key=line.Substring(1,line.Length-2);continue;}if(key!=null&&!String.IsNullOrWhiteSpace(line)){result[key]=line;key=null;}}}return result;}
    internal static string Translate(string text){if(text==null)return "";string value;return Greek.TryGetValue(text.Trim(),out value)?value:text;}
    static string ThemePath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"netmind","nCall","ui-theme.txt");}}
    static string ReadMode(){try{var s=File.ReadAllText(ThemePath).Trim();return new[]{"light","dark","system"}.Contains(s)?s:"light";}catch{return "light";}}
    internal static bool Dark {get{if(Mode!="system")return Mode=="dark";try{using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))return Convert.ToInt32(k==null?1:k.GetValue("AppsUseLightTheme",1))==0;}catch{return false;}}}
    internal static void SetMode(string mode){Mode=mode;try{Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));File.WriteAllText(ThemePath,mode);}catch{}ApplyPalette();foreach(var s in surfaces.Values.ToArray())s.TitleTheme();}
    // Light: netmind paper. Dark: netmind navy (#12233f -> #070a12), as on the proposal covers.
    // Border = control outlines (>= 3:1), Line = dividers, Tint = hover, Selected = selection,
    // Primary = filled buttons (white text >= 4.5:1).
    static readonly string[] PaletteKeys={"Canvas","Paper","Ink","Muted","Line","Border","Tint","Selected","Primary","Accent"};
    static readonly string[] LightPalette={"#f7f9fc","#ffffff","#141414","#5f6b7a","#e5e7eb","#8a94a6","#eff5ff","#dbeafe","#2563eb","#3b82f6"};
    static readonly string[] DarkPalette={"#0b1626","#12233f","#e8edf5","#a3b0c4","#22375a","#6b7fa0","#172b4d","#1e3f78","#2563eb","#3b82f6"};
    internal static Color Canvas{get{return (Color)ColorConverter.ConvertFromString(Dark?DarkPalette[0]:LightPalette[0]);}}
    internal static void Caption(IntPtr h){int dark=Dark?1:0;Native.DwmSetWindowAttribute(h,20,ref dark,4);var c=Canvas;int caption=c.R|(c.G<<8)|(c.B<<16);Native.DwmSetWindowAttribute(h,35,ref caption,4);}
    internal static void ApplyPalette(){var values=Dark?DarkPalette:LightPalette;for(int i=0;i<PaletteKeys.Length;i++){var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(values[i]));brush.Freeze();Theme[PaletteKeys[i]]=brush;}}
    internal static void Error(Exception e){try{Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));File.AppendAllText(Path.Combine(Path.GetDirectoryName(ThemePath),"ui-error.log"),DateTime.Now.ToString("s")+" "+e.GetType().FullName+": "+e.Message+"\n"+e.StackTrace+"\n\n");}catch{}}
    // nCall profile folder (where ncall.ini is), sent by the native side at start-up.
    internal static string ProfileFolder;
    public static int Profile(string path){ProfileFolder=path;return 1;}
    // Saved accounts, sent by the native side whenever they change: first line the active id, then "id<TAB>label".
    static volatile string accounts="0";
    public static int Accounts(string data){accounts=data??"0";return 1;}
    internal static int ActiveAccount{get{int id;int.TryParse(accounts.Split('\n')[0],out id);return id;}}
    internal static List<KeyValuePair<int,string>> AccountList(){
        var list=new List<KeyValuePair<int,string>>();
        foreach(string line in accounts.Split('\n').Skip(1)){int tab=line.IndexOf('\t');int id;if(tab>0&&int.TryParse(line.Substring(0,tab),out id))list.Add(new KeyValuePair<int,string>(id,line.Substring(tab+1)));}
        return list;
    }
    public static int Initialize(string argument){try{var bits=argument.Split('|');var hwnd=new IntPtr(long.Parse(bits[0]));int id=int.Parse(bits[1]);if(!Native.Own(hwnd)||surfaces.ContainsKey(hwnd))return 0;ApplyPalette();surfaces[hwnd]=new Surface(hwnd,id);return 1;}catch(Exception e){Error(e);return -1;}}
    public static int FilterMessage(string pointer){try{var p=new IntPtr(long.Parse(pointer));var m=(MSG)Marshal.PtrToStructure(p,typeof(MSG));return ComponentDispatcher.RaiseThreadMessage(ref m)?1:0;}catch{return 0;}}
    public static int Detach(string arg){try{Surface s;var h=new IntPtr(long.Parse(arg));if(surfaces.TryGetValue(h,out s)){surfaces.Remove(h);s.Dispose();}}catch(Exception e){Error(e);}return 0;}
    internal static string Version{get{try{return System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName).ProductVersion;}catch{return "";}}}
    // Called by CmainDlg::CheckUpdates: "hwnd|1" when the user asked (shows "no update" too).
    public static int CheckUpdate(string arg){try{var parts=arg.Split('|');Updater.Check(new IntPtr(long.Parse(parts[0])),parts.Length>1&&parts[1]=="1");return 1;}catch(Exception e){Error(e);return -1;}}
    internal static void ExitApplication(){var main=surfaces.Keys.FirstOrDefault(k=>surfaces[k].IsMain);if(main!=IntPtr.Zero)Native.Command(main,32784);}
    internal static bool CallInProgress{get{return surfaces.Values.Any(x=>x.InCall);}}
    internal static Window Popup(string title,FrameworkElement body,IntPtr owner){return Dialog(title,body,owner);}
    // Dial through the main window's own number field and Call button.
    internal static void Dial(string number){var main=surfaces.Values.FirstOrDefault(x=>x.IsMain);if(main!=null)main.Dial(number);}
    public static int About(string arg){try{
        var h=new IntPtr(long.Parse(arg));var panel=new StackPanel{Margin=new Thickness(28)};
        var title=Surface.ProductMark(28);title.Margin=new Thickness(0,0,0,12);panel.Children.Add(title);
        panel.Children.Add(Surface.Label("Έκδοση "+Version,13));
        panel.Children.Add(Surface.Label("Τηλεφωνία SIP για Windows",16));
        panel.Children.Add(Surface.Label("www.netmind.gr\ninfo@netmind.gr\n2331200260",14));
        var legal=Surface.Label("Βασίζεται στα MicroSIP (© 2011-2026 MicroSIP) και PJSIP (© Teluu Inc.). Διανέμεται με την άδεια GNU GPL v3 ή νεότερη, χωρίς καμία εγγύηση. Δείτε τα αρχεία COPYING-GPL-3 και NOTICE.txt.",11);
        legal.SetResourceReference(TextBlock.ForegroundProperty,"Muted");legal.Margin=new Thickness(0,8,0,14);panel.Children.Add(legal);
        var w=Dialog("Σχετικά με το nCall",panel,h);
        var actions=new UniformGrid{Columns=2};
        var support=Surface.Button("Κλήση στη netmind",()=>{w.Close();Dial("2331200260");},true);support.Margin=new Thickness(0,4,4,4);actions.Children.Add(support);
        var close=Surface.Button("Κλείσιμο",()=>w.Close());close.IsCancel=true;close.IsDefault=true;close.Margin=new Thickness(4,4,0,4);actions.Children.Add(close);
        panel.Children.Add(actions);w.ShowDialog();return 1;
    }catch(Exception e){Error(e);return -1;}}
    public static int Message(string arg){try{var parts=arg.Split('|');var h=new IntPtr(long.Parse(parts[0]));uint type=uint.Parse(parts[1]);string text=Encoding.UTF8.GetString(Convert.FromBase64String(parts[2]));int result=2;var panel=new StackPanel{Margin=new Thickness(24)};panel.Children.Add(Surface.Label(text,14));var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,20,0,0)};panel.Children.Add(actions);var w=Dialog("nCall",panel,h);int kind=(int)(type&15);var ids=kind==4?new[]{6,7}:kind==3?new[]{6,7,2}:kind==1?new[]{1,2}:kind==5?new[]{4,2}:kind==2?new[]{3,4,5}:new[]{1};foreach(int id in ids){int choice=id;string name=id==6?"Ναι":id==7?"Όχι":id==2?"Ακύρωση":id==3?"Διακοπή":id==4?"Επανάληψη":id==5?"Παράβλεψη":"Εντάξει";var b=Surface.Button(name,()=>{result=choice;w.Close();},id==ids[0]);b.Margin=new Thickness(5);b.IsDefault=(type&0x300)==((Array.IndexOf(ids,id))<<8);b.IsCancel=id==2;actions.Children.Add(b);}w.ShowDialog();return result;}catch(Exception e){Error(e);return -1;}}
    static Window Dialog(string title,FrameworkElement body,IntPtr owner){ApplyPalette();var layout=new DockPanel();var footer=Surface.BrandFooter();DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);layout.Children.Add(body);var w=new Window{Title=title,Content=layout,Width=460,SizeToContent=SizeToContent.Height,MaxHeight=740,WindowStartupLocation=owner==IntPtr.Zero?WindowStartupLocation.CenterScreen:WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,FontFamily=Surface.BodyFont,FontSize=13};w.Resources.MergedDictionaries.Add(Theme);w.SetResourceReference(Window.BackgroundProperty,"Canvas");w.SetResourceReference(Window.ForegroundProperty,"Ink");if(Native.Own(owner))new WindowInteropHelper(w).Owner=owner;w.SourceInitialized+=(s,e)=>Caption(new WindowInteropHelper(w).Handle);return w;}
}
// Updates from the GitHub releases of netmindgr/ncall:
// latest.json {"version","url","sha256","notes"} -> download -> SHA-256 check ->
// wait until no call is in progress -> silent install (the installer restarts nCall).
internal static class Updater {
    const string Feed="https://github.com/netmindgr/ncall/releases/latest/download/latest.json";
    const string Downloads="https://github.com/netmindgr/ncall/releases/download/";
    static bool busy;
    static string Field(string json,string name){var m=System.Text.RegularExpressions.Regex.Match(json,"\""+name+"\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");return m.Success?System.Text.RegularExpressions.Regex.Unescape(m.Groups[1].Value):"";}
    static Version Parse(string v){Version r;return Version.TryParse((v??"").Trim().TrimStart('v','V'),out r)?r:null;}
    internal static void Check(IntPtr owner,bool interactive){
        if(busy)return;busy=true;var ui=Dispatcher.CurrentDispatcher;
        System.Threading.ThreadPool.QueueUserWorkItem(_=>{
            string json=null;Exception failure=null;
            try{
                System.Net.ServicePointManager.SecurityProtocol=System.Net.SecurityProtocolType.Tls12|(System.Net.SecurityProtocolType)12288;
                using(var web=new System.Net.WebClient()){web.Headers.Add("User-Agent","nCall/"+Host.Version);web.Encoding=Encoding.UTF8;json=web.DownloadString(Feed);}
            }catch(Exception e){failure=e;}
            ui.BeginInvoke(new Action(()=>{try{Offer(owner,interactive,json,failure);}catch(Exception e){Host.Error(e);}finally{busy=false;}}));
        });
    }
    static void Offer(IntPtr owner,bool interactive,string json,Exception failure){
        if(json==null){if(interactive)Info(owner,"Δεν ήταν δυνατός ο έλεγχος για ενημερώσεις.\nΕλέγξτε τη σύνδεση στο διαδίκτυο και δοκιμάστε ξανά.");return;}
        Version latest=Parse(Field(json,"version")),current=Parse(Host.Version);string url=Field(json,"url"),sha=Field(json,"sha256").ToLowerInvariant(),notes=Field(json,"notes");
        bool valid=latest!=null&&url.StartsWith(Downloads,StringComparison.Ordinal)&&url.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)&&System.Text.RegularExpressions.Regex.IsMatch(sha,"^[0-9a-f]{64}$");
        if(!valid||current==null||latest<=current){if(interactive)Info(owner,"Έχετε την τελευταία έκδοση ("+Host.Version+").");return;}
        var panel=new StackPanel{Margin=new Thickness(24)};
        var title=Surface.Label("Νέα έκδοση "+latest,19);title.FontWeight=FontWeights.SemiBold;panel.Children.Add(title);
        panel.Children.Add(Surface.Label("Έχετε την έκδοση "+Host.Version+". Η εγκατάσταση γίνεται αυτόματα και το nCall ανοίγει ξανά.",13));
        if(notes!=""){var n=Surface.Label(notes,12);n.SetResourceReference(TextBlock.ForegroundProperty,"Muted");panel.Children.Add(n);}
        var w=Host.Popup("Ενημέρωση nCall",panel,owner);var actions=new UniformGrid{Columns=2,Margin=new Thickness(0,12,0,0)};
        var install=Surface.Button("Εγκατάσταση",()=>{w.Close();Download(owner,url,sha);},true);install.Margin=new Thickness(0,0,4,0);install.IsDefault=true;actions.Children.Add(install);
        var later=Surface.Button("Αργότερα",()=>w.Close());later.Margin=new Thickness(4,0,0,0);later.IsCancel=true;actions.Children.Add(later);
        panel.Children.Add(actions);w.ShowDialog();
    }
    static void Download(IntPtr owner,string url,string sha){
        var ui=Dispatcher.CurrentDispatcher;string folder=Path.Combine(Path.GetTempPath(),"nCall-update");string file=Path.Combine(folder,Path.GetFileName(new Uri(url).LocalPath));
        System.Threading.ThreadPool.QueueUserWorkItem(_=>{
            string error=null;
            try{
                Directory.CreateDirectory(folder);
                using(var web=new System.Net.WebClient()){web.Headers.Add("User-Agent","nCall/"+Host.Version);web.DownloadFile(url,file);}
                string actual;using(var hash=System.Security.Cryptography.SHA256.Create())using(var stream=File.OpenRead(file))actual=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
                if(actual!=sha){File.Delete(file);error="Το αρχείο της ενημέρωσης δεν είναι ακέραιο (SHA-256). Η εγκατάσταση ακυρώθηκε.";}
            }catch(Exception e){Host.Error(e);error="Η λήψη της ενημέρωσης απέτυχε.";}
            ui.BeginInvoke(new Action(()=>{if(error!=null)Info(owner,error);else InstallWhenIdle(file);}));
        });
    }
    static void InstallWhenIdle(string file){
        // Never interrupt a call: wait until no call is ringing or active.
        if(!Host.CallInProgress){Install(file);return;}
        var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(10)};
        timer.Tick+=(s,e)=>{if(Host.CallInProgress)return;timer.Stop();Install(file);};timer.Start();
    }
    static void Install(string file){
        // The installer runs after nCall has exited (its mutex is checked), then starts nCall again.
        string args="/c ping -n 4 127.0.0.1 >nul & \""+file+"\" /SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"cmd.exe"),args){WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden,CreateNoWindow=true,UseShellExecute=false});
        Host.ExitApplication();
    }
    static void Info(IntPtr owner,string text){
        var panel=new StackPanel{Margin=new Thickness(24)};panel.Children.Add(Surface.Label(text,13));
        var w=Host.Popup("Ενημέρωση nCall",panel,owner);var ok=Surface.Button("Εντάξει",()=>w.Close(),true);ok.IsDefault=true;ok.IsCancel=true;ok.Margin=new Thickness(0,12,0,0);panel.Children.Add(ok);w.ShowDialog();
    }
}
// Row of the history / contacts list: first column on the left, the rest (number, details) right-aligned.
public sealed class ListRow {
    readonly string text;
    public string Title{get;private set;}
    public string Detail{get;private set;}
    internal string[] Cells{get;private set;}
    internal ListRow(string[] cells){
        var parts=cells.Where(x=>!String.IsNullOrEmpty(x)).ToArray();Cells=parts;
        text=String.Join("  ·  ",parts);
        Title=parts.Length>0?parts[0]:"";
        Detail=String.Join("  ·  ",parts.Skip(1).Where(x=>x!=Title));
    }
    public override string ToString(){return text;}
    internal static DataTemplate Template(){
        var row=new FrameworkElementFactory(typeof(DockPanel));
        var detail=new FrameworkElementFactory(typeof(TextBlock));
        detail.SetValue(DockPanel.DockProperty,Dock.Right);
        detail.SetValue(FrameworkElement.MarginProperty,new Thickness(12,0,0,0));
        detail.SetResourceReference(TextBlock.ForegroundProperty,"Muted");
        detail.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("Detail"));
        var title=new FrameworkElementFactory(typeof(TextBlock));
        title.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);
        title.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("Title"));
        row.AppendChild(detail);row.AppendChild(title);
        return new DataTemplate{VisualTree=row};
    }
}
internal sealed class Surface : IDisposable {
    internal static readonly FontFamily BodyFont=new FontFamily(new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)+"/fonts/"),"./#Open Sans");
    internal static readonly FontFamily LogoFont=new FontFamily(new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)+"/fonts/"),"./#Comfortaa");
    readonly IntPtr parent;readonly int dialogId;readonly HwndSource source;readonly DispatcherTimer timer;
    readonly List<Action> refresh=new List<Action>();List<ControlInfo> controls;DockPanel root;Grid body;int selectedTab=-1;bool syncing,disposed;string currentSection="";bool lastDark;
    // A child HwndSource inside a layered (transparent) parent does not paint with hardware rendering;
    // the incoming-call window is shown before attaching, so the parent is made opaque first.
    // A window that was already visible when the surface attached is announced to WPF with a hide/show of
    // the surface itself; otherwise WPF keeps treating it as hidden and never renders (incoming-call window).
    internal Surface(IntPtr h,int id){parent=h;dialogId=id;Native.RECT rc;Native.GetClientRect(h,out rc);if((Native.GetWindowLong(h,-20)&0x80000)!=0){Native.SetLayeredWindowAttributes(h,0,255,2);Native.SetWindowLong(h,-20,Native.GetWindowLong(h,-20)&~0x80000);}Native.SetWindowLong(h,-16,Native.GetWindowLong(h,-16)|0x02000000);Native.EnumChildWindows(h,(child,p)=>{Native.SetWindowLong(child,-16,Native.GetWindowLong(child,-16)|0x04000000);return true;},IntPtr.Zero);var parameters=new HwndSourceParameters("netmind modern UI"){ParentWindow=h,WindowStyle=0x56000000,Width=rc.Right,Height=rc.Bottom};source=new HwndSource(parameters);try{source.CompositionTarget.BackgroundColor=Host.Canvas;Native.SetWindowPos(source.Handle,IntPtr.Zero,0,0,0,0,0x13);Build();}catch{source.Dispose();throw;}Native.GuardBackendPainting(parent,source.Handle);if(Native.IsWindowVisible(parent))root.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{if(disposed||!Native.IsWindowVisible(parent))return;bool front=Native.GetForegroundWindow()==parent;Native.ShowWindow(parent,0);Native.ShowWindow(parent,front?5:8);}));Native.SetLayeredWindowAttributes(parent,0,255,2);Native.SetWindowLong(parent,-20,Native.GetWindowLong(parent,-20)&~0x80000);timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(180)};timer.Tick+=(s,e)=>Tick();timer.Start();TitleTheme();}
    internal static TextBlock Label(string text,double size=13){var t=new TextBlock{Text=Host.Translate(text),FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,9)};t.SetResourceReference(TextBlock.ForegroundProperty,"Ink");return t;}
    internal static TextBlock Wordmark(double size=16){
        var logo=Label("netmind",size);logo.FontFamily=LogoFont;logo.FontWeight=FontWeights.Bold;
        logo.Margin=new Thickness(0);logo.HorizontalAlignment=HorizontalAlignment.Center;
        var dotRun=new Run(".");dotRun.SetResourceReference(TextElement.ForegroundProperty,"Accent");logo.Inlines.Add(dotRun);
        System.Windows.Automation.AutomationProperties.SetName(logo,"netmind");return logo;
    }
    internal static StackPanel ProductMark(double size,bool byline=true){
        var name=new TextBlock{FontSize=size,FontWeight=FontWeights.SemiBold};
        var n=new Run("n");n.SetResourceReference(TextElement.ForegroundProperty,"Accent");name.Inlines.Add(n);
        var call=new Run("Call");call.SetResourceReference(TextElement.ForegroundProperty,"Ink");name.Inlines.Add(call);
        var mark=new StackPanel{VerticalAlignment=VerticalAlignment.Center};mark.Children.Add(name);
        if(byline){
            var by=new TextBlock{FontSize=Math.Max(11,size*0.46),Margin=new Thickness(1,-2,0,0)};
            var prefix=new Run("by ");prefix.SetResourceReference(TextElement.ForegroundProperty,"Muted");by.Inlines.Add(prefix);
            var nm=new Run("netmind"){FontFamily=LogoFont,FontWeight=FontWeights.Bold};nm.SetResourceReference(TextElement.ForegroundProperty,"Ink");by.Inlines.Add(nm);
            var dot=new Run("."){FontFamily=LogoFont,FontWeight=FontWeights.Bold};dot.SetResourceReference(TextElement.ForegroundProperty,"Accent");by.Inlines.Add(dot);
            mark.Children.Add(by);
        }
        System.Windows.Automation.AutomationProperties.SetName(mark,"nCall by netmind");return mark;
    }
    internal static Border BrandFooter(){
        var footer=new Border{BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(22,12,22,14),Child=Wordmark()};
        footer.SetResourceReference(Border.BorderBrushProperty,"Line");return footer;
    }
    internal static Button Button(string name,Action action,bool primary=false){var b=new Button{Content=name,Margin=new Thickness(0,4,0,4)};if(primary){b.SetResourceReference(Control.BackgroundProperty,"Primary");b.SetResourceReference(Control.BorderBrushProperty,"Primary");b.Foreground=Brushes.White;}b.Click+=(s,e)=>action();return b;}
    internal void TitleTheme(){Host.Caption(parent);lastDark=Host.Dark;}
    void Tick(){if(!Native.IsWindow(parent)){Host.Detach(parent.ToInt64().ToString());return;}try{if(!Native.IsWindowVisible(parent))return;if(controls.Any(c=>!Native.IsWindow(c.H))){Build();return;}Native.RECT rc;Native.GetClientRect(parent,out rc);Native.SetWindowPos(source.Handle,IntPtr.Zero,0,0,rc.Right,rc.Bottom,0x10);if(lastDark!=Host.Dark){Host.ApplyPalette();TitleTheme();source.CompositionTarget.BackgroundColor=Host.Canvas;}if(dialogId==101){var tab=Find(1000,"SysTabControl32");int n=tab==null?0:Native.Value(tab.H,0x130b);if(n!=selectedTab){Build();return;}}syncing=true;foreach(var update in refresh.ToArray())update();syncing=false;}catch(Exception e){syncing=false;Host.Error(e);}}
    List<ControlInfo> ReadControls(){var found=new List<ControlInfo>();Native.EnumChildWindows(parent,(h,p)=>{string cls=Native.Class(h);if(cls.StartsWith("HwndWrapper",StringComparison.Ordinal)||!Native.Own(h))return true;var up=Native.GetParent(h);if(Native.Class(up).StartsWith("HwndWrapper",StringComparison.Ordinal)||Native.Class(up)=="ComboBox")return true;Native.RECT rc;Native.GetWindowRect(h,out rc);if(cls=="Button"||cls=="Static"||cls=="Edit"||cls=="ComboBox"||cls=="ListBox"||cls=="SysLink"||cls=="SysTabControl32"||cls=="SysListView32"||cls.StartsWith("RichEdit",StringComparison.OrdinalIgnoreCase)||cls.StartsWith("msctls_"))found.Add(new ControlInfo{H=h,Id=Native.GetDlgCtrlID(h),Style=Native.GetWindowLong(h,-16),Class=cls,Caption=Native.Text(h),Rect=rc});return true;},IntPtr.Zero);return found;}
    bool Visible(IntPtr h){
        // Evaluate the logical control state even while its new parent is waiting
        // to show the completed WPF surface. Root visibility is deliberately excluded.
        while(h!=IntPtr.Zero&&h!=parent){if((Native.GetWindowLong(h,-16)&0x10000000)==0)return false;h=Native.GetParent(h);}return h==parent;
    }
    ControlInfo Find(int id,string cls=null){return controls.FirstOrDefault(c=>c.Id==id&&(cls==null||c.Class==cls));}
    void Build(){controls=ReadControls();refresh.Clear();root=new DockPanel{LastChildFill=true};root.Resources.MergedDictionaries.Add(Host.Theme);root.SetResourceReference(Panel.BackgroundProperty,"Canvas");root.SetValue(TextElement.FontFamilyProperty,BodyFont);root.SetValue(TextElement.FontSizeProperty,13.0);body=new Grid{Margin=new Thickness(22,0,22,14)};Header();var footer=BrandFooter();DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);if(dialogId==101)BuildMain();else if(dialogId==112)BuildRingin();else if(dialogId==102)BuildCall();else if(dialogId==113)BuildTransfer();else BuildForm();root.Children.Add(body);root.KeyDown+=(s,e)=>{
    if(e.Handled)return;
    if((Keyboard.Modifiers&ModifierKeys.Control)!=0){int command=e.Key==Key.P?32789:e.Key==Key.S?32798:0;if(command!=0){IntPtr target=parent;while(Native.Own(Native.GetParent(target)))target=Native.GetParent(target);Native.Command(target,command);e.Handled=true;}}
    // Esc in the call (102) or incoming-call (112) window would hang up or reject the call.
    if(dialogId!=101&&dialogId!=102&&dialogId!=112&&e.Key==Key.Escape){Native.Command(parent,2);e.Handled=true;}
};source.RootVisual=root;FocusSurface();}
    void FocusSurface(){
        // Keyboard input belongs to the visible WPF surface, not to the hidden native controls.
        if(Native.GetForegroundWindow()!=Native.GetAncestor(parent,2))return;
        Native.SetFocus(source.Handle);
        root.Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=>root.MoveFocus(new TraversalRequest(FocusNavigationDirection.First))));
    }
    void Header(){
        var top=new DockPanel{Margin=new Thickness(22,18,22,16)};
        var logo=ProductMark(dialogId==101?25:20);
        if(dialogId==112)return;
        if(dialogId!=101){top.Children.Add(logo);DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);return;}
        var themes=Button("",()=>{});themes.Width=40;themes.Height=38;themes.Padding=new Thickness(4);themes.Margin=new Thickness(8,0,0,0);themes.HorizontalAlignment=HorizontalAlignment.Right;
        var symbol=new TextBlock{FontFamily=new FontFamily("Segoe UI Symbol"),FontSize=20,VerticalAlignment=VerticalAlignment.Center};symbol.SetResourceReference(TextBlock.ForegroundProperty,"Ink");themes.Content=symbol;
        Action update=()=>{symbol.Text=Host.Mode=="dark"?"☾":Host.Mode=="system"?"◐":"☀";themes.ToolTip="Θέμα: "+(Host.Mode=="dark"?"Σκοτεινό":Host.Mode=="system"?"Αυτόματο βάσει Windows":"Φωτεινό");System.Windows.Automation.AutomationProperties.SetName(themes,"Επιλογή θέματος");};update();refresh.Add(update);
        var menu=new ContextMenu();menu.Resources.MergedDictionaries.Add(Host.Theme);foreach(var pair in new[]{new[]{"☀  Φωτεινό","light"},new[]{"☾  Σκοτεινό","dark"},new[]{"◐  Αυτόματο βάσει Windows","system"}}){var choice=new MenuItem{Header=pair[0]};string mode=pair[1];choice.Click+=(s,e)=>Host.SetMode(mode);menu.Items.Add(choice);}themes.ContextMenu=menu;themes.Click+=(s,e)=>{menu.PlacementTarget=themes;menu.IsOpen=true;};
        var more=Button("",()=>{});more.Width=40;more.Height=38;more.Padding=new Thickness(4);more.Margin=new Thickness(8,0,0,0);
        var lines=new TextBlock{Text="\u2630",FontFamily=new FontFamily("Segoe UI Symbol"),FontSize=18,VerticalAlignment=VerticalAlignment.Center};lines.SetResourceReference(TextBlock.ForegroundProperty,"Ink");more.Content=lines;
        more.ToolTip="Μενού";System.Windows.Automation.AutomationProperties.SetName(more,"Μενού");
        var main=new ContextMenu();main.Resources.MergedDictionaries.Add(Host.Theme);
        Action<string,Action> item=(text,act)=>{var mi=new MenuItem{Header=text};mi.Click+=(s,e)=>act();main.Items.Add(mi);};
        // Saved accounts at the top (✓ = active); one click switches. The list comes from the native side (Host.Accounts).
        var accountItems=new List<object>();
        main.Opened+=(s,e)=>{
            foreach(var old in accountItems)main.Items.Remove(old);accountItems.Clear();
            int at=0,active=Host.ActiveAccount;
            foreach(var account in Host.AccountList()){int id=account.Key;
                var mi=new MenuItem{Header=(id==active?"✓  ":"   ")+account.Value,ToolTip=id==active?"Ενεργός λογαριασμός":"Ενεργοποίηση αυτού του λογαριασμού"};
                if(id==active)mi.FontWeight=FontWeights.SemiBold;
                mi.Click+=(x,y)=>{if(id!=Host.ActiveAccount)Native.Command(parent,40000+id-1);};
                main.Items.Insert(at++,mi);accountItems.Add(mi);}
            if(active>0){var off=new MenuItem{Header="Αποσύνδεση λογαριασμού"};off.Click+=(x,y)=>Native.Command(parent,40000+Host.ActiveAccount-1);main.Items.Insert(at++,off);accountItems.Add(off);}
            if(at>0){var line=new Separator();main.Items.Insert(at++,line);accountItems.Add(line);}
        };
        item("Επεξεργασία λογαριασμού",()=>{var c=Find(6004);if(c!=null)Native.Value(c.H,0xf5);});
        item("Νέος λογαριασμός…",()=>Native.Command(parent,32771));
        main.Items.Add(new Separator());
        item("Νέο SMS…",()=>Sms.Compose(parent,"",""));
        item("Ιστορικό SMS…",()=>Sms.History(parent));
        item("Ρυθμίσεις SMS…",()=>Sms.Settings(parent));
        main.Items.Add(new Separator());
        item("Ρυθμίσεις",()=>Native.Command(parent,32789));
        item("Έλεγχος για ενημερώσεις",()=>Native.Command(parent,32813));
        item("Σχετικά με το nCall",()=>Host.About(parent.ToInt64().ToString()));
        main.Items.Add(new Separator());
        item("Έξοδος",()=>Native.Command(parent,32784));
        more.ContextMenu=main;more.Click+=(s,e)=>{main.PlacementTarget=more;main.IsOpen=true;};
        DockPanel.SetDock(more,Dock.Right);top.Children.Add(more);
        DockPanel.SetDock(themes,Dock.Right);top.Children.Add(themes);top.Children.Add(logo);DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
    }
    void BuildMain(){var tab=Find(1000,"SysTabControl32");selectedTab=tab==null?0:Native.Value(tab.H,0x130b);body.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});body.RowDefinitions.Add(new RowDefinition());var panel=new StackPanel();var status=Label("",12);status.SetResourceReference(TextBlock.ForegroundProperty,"Muted");status.Margin=new Thickness(0);
        // Coloured dot before the registration status: green connected, amber connecting / DND, red error, grey offline.
        var dot=new System.Windows.Shapes.Ellipse{Width=9,Height=9,Margin=new Thickness(1,0,7,0),VerticalAlignment=VerticalAlignment.Center};
        var statusRow=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,9)};statusRow.Children.Add(dot);statusRow.Children.Add(status);
        System.Windows.Automation.AutomationProperties.SetLiveSetting(status,System.Windows.Automation.AutomationLiveSetting.Polite);
        refresh.Add(()=>{var c=Find(6005);string text=c==null?"Χωρίς λογαριασμό":Host.Translate(Native.Text(c.H));if(status.Text!=text)status.Text=text;
            Color color=StatusColor(text);var current=dot.Fill as SolidColorBrush;if(current==null||current.Color!=color){var brush=new SolidColorBrush(color);brush.Freeze();dot.Fill=brush;}});
        panel.Children.Add(statusRow);var nav=new UniformGrid{Columns=3,Margin=new Thickness(0,5,0,16)};string[] names={"Κλήση","Ιστορικό","Επαφές"};for(int i=0;i<3;i++){int n=i;var b=Button(names[i],()=>{if(tab!=null){Native.Notify(tab.H,-552);Native.SendMessage(tab.H,0x130c,new IntPtr(n),IntPtr.Zero);Native.Notify(tab.H,-551);}},i==selectedTab);b.Margin=new Thickness(2);nav.Children.Add(b);}nav.Margin=new Thickness(0,5,0,12);panel.Children.Add(nav);body.Children.Add(panel);if(selectedTab==0){var content=new StackPanel();var sc=new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(sc,1);body.Children.Add(sc);Phone(content);}
        // History / contacts: the list fills the remaining height and scrolls by itself (a list inside a ScrollViewer never scrolls).
        else{var lists=new DockPanel();Grid.SetRow(lists,1);body.Children.Add(lists);Listing(lists,selectedTab==1?1027:1030);}}
    // Call / conversation window (IDD_MESSAGES): one view per call, switcher for several calls.
    void BuildCall(){
        body.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});body.RowDefinitions.Add(new RowDefinition());body.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        var top=new StackPanel();
        var tabs=Find(1000,"SysTabControl32");
        if(tabs!=null){
            var switcher=new WrapPanel{Margin=new Thickness(0,0,0,10)};System.Windows.Automation.AutomationProperties.SetName(switcher,"Κλήσεις");string signature=null;
            refresh.Add(()=>{int count=Native.Value(tabs.H,0x1304);int current=Native.Value(tabs.H,0x130b);var names=Enumerable.Range(0,Math.Max(0,count)).Select(i=>Native.TabText(tabs.H,i)).ToArray();string sign=current+"|"+String.Join("\n",names);
                if(sign==signature)return;signature=sign;switcher.Children.Clear();switcher.Visibility=count>1?Visibility.Visible:Visibility.Collapsed;
                for(int i=0;i<names.Length;i++){int n=i;var b=Button(names[i],()=>{Native.Notify(tabs.H,-552);Native.SendMessage(tabs.H,0x130c,new IntPtr(n),IntPtr.Zero);Native.Notify(tabs.H,-551);},i==current);b.Margin=new Thickness(0,0,6,6);b.Padding=new Thickness(10,6,10,6);switcher.Children.Add(b);}});
            top.Children.Add(switcher);
        }
        var who=Label("",24);who.FontWeight=FontWeights.SemiBold;who.Margin=new Thickness(0,0,0,12);refresh.Add(()=>{string t=Native.Text(parent);if(who.Text!=t)who.Text=t;});top.Children.Add(who);
        var actions=new WrapPanel{Margin=new Thickness(0,0,0,12)};
        foreach(var spec in new[]{new object[]{1023,null},new object[]{1057,"Αναμονή"},new object[]{1135,"Μεταφορά"},new object[]{1159,"Συνδιάσκεψη"},new object[]{1055,"Τερματισμός"}}){
            var c=Find((int)spec[0],"Button");if(c==null)continue;var b=BoundButton(c);if(spec[1]!=null){string label=(string)spec[1];b.Content=label;refresh.Add(()=>{if(b.Content as string==""||b.Content==null)b.Content=label;});}
            b.Margin=new Thickness(0,0,8,8);b.MinWidth=104;b.Padding=new Thickness(14,10,14,10);
            if(c.Id==1055){var red=new SolidColorBrush(Color.FromRgb(220,38,38));red.Freeze();b.Background=red;b.BorderBrush=red;b.Foreground=Brushes.White;}
            if(c.Id==1023){var green=new SolidColorBrush(Color.FromRgb(21,128,61));green.Freeze();b.Background=green;b.BorderBrush=green;b.Foreground=Brushes.White;}
            actions.Children.Add(b);
        }
        top.Children.Add(actions);body.Children.Add(top);
        var history=Find(1004);
        if(history!=null){
            var log=new TextBox{IsReadOnly=true,TextWrapping=TextWrapping.Wrap,AcceptsReturn=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MinHeight=120};
            System.Windows.Automation.AutomationProperties.SetName(log,"Συνομιλία");
            refresh.Add(()=>{string t=Native.Text(history.H);if(log.Text!=t){log.Text=t;log.ScrollToEnd();}});
            Grid.SetRow(log,1);body.Children.Add(log);
        }
        var input=Find(1076);
        if(input!=null){
            var row=new DockPanel{Margin=new Thickness(0,10,0,0)};
            var box=new TextBox{AcceptsReturn=false};System.Windows.Automation.AutomationProperties.SetName(box,"Μήνυμα");
            Action send=()=>{string text=box.Text.Trim();if(text==""||!Native.IsWindowEnabled(input.H))return;Native.SendText(input.H,0x0c,IntPtr.Zero,text);Native.PostMessage(input.H,0x102,new IntPtr(13),IntPtr.Zero);box.Text="";};
            box.KeyDown+=(s,e)=>{if(e.Key==Key.Enter){send();e.Handled=true;}};
            var go=Button("Αποστολή",send,true);go.Margin=new Thickness(8,0,0,0);DockPanel.SetDock(go,Dock.Right);row.Children.Add(go);row.Children.Add(box);
            refresh.Add(()=>{bool on=Native.IsWindowEnabled(input.H);box.IsEnabled=on;go.IsEnabled=on;});
            Grid.SetRow(row,2);body.Children.Add(row);
        }
    }
    static readonly Color Green=Color.FromRgb(22,163,74),Amber=Color.FromRgb(217,119,6),Red=Color.FromRgb(220,38,38),Grey=Color.FromRgb(148,163,184);
    static Color StatusColor(string text){
        Func<string,bool> isAny=keys=>keys.Split('|').Any(k=>String.Equals(text,Host.Translate(k),StringComparison.CurrentCultureIgnoreCase));
        if(text==""||text=="Χωρίς λογαριασμό"||isAny("Offline"))return Grey;
        if(text.StartsWith(Host.Translate("Connecting"),StringComparison.CurrentCultureIgnoreCase)||isAny("Do Not Disturb|Away"))return Amber;
        if(isAny("Online|Idle|Call Forwarding|Auto Answer|Ringing|Calling|Incoming Call|On Hold|Busy|Session Progress"))return Green;
        return Red;
    }
    internal bool IsMain{get{return dialogId==101;}}
    // Ringing window shown, or the main window's End button visible (active call).
    internal bool InCall{get{if(!Native.IsWindow(parent))return false;if(dialogId==112)return Native.IsWindowVisible(parent);if(dialogId!=101)return false;var end=Find(1055,"Button");return end!=null&&Visible(end.H);}}
    internal void Dial(string number){
        var tab=Find(1000,"SysTabControl32");if(tab!=null&&selectedTab!=0){Native.Notify(tab.H,-552);Native.SendMessage(tab.H,0x130c,IntPtr.Zero,IntPtr.Zero);Native.Notify(tab.H,-551);}
        var field=Find(1078,"ComboBox");var call=Find(1022,"Button");if(field==null||call==null)return;Write(field,number);Native.Value(call.H,0xf5);
    }
    void BuildRingin(){
        var panel=new StackPanel{VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,8,0,0)};
        var kind=Label("Εισερχόμενη κλήση",12);kind.SetResourceReference(TextBlock.ForegroundProperty,"Muted");kind.TextAlignment=TextAlignment.Center;panel.Children.Add(kind);
        var name=Label("",20);name.FontWeight=FontWeights.SemiBold;name.TextAlignment=TextAlignment.Center;name.TextTrimming=TextTrimming.CharacterEllipsis;name.Margin=new Thickness(0,2,0,2);panel.Children.Add(name);
        var address=Label("",13);address.SetResourceReference(TextBlock.ForegroundProperty,"Muted");address.TextAlignment=TextAlignment.Center;address.Margin=new Thickness(0,0,0,10);panel.Children.Add(address);
        // The native caller text holds the SIP address, user agent, "To: <own account>" and diversion; only the
        // number (when a contact name is shown) and the diversion are useful here, the rest stays in the tooltip.
        refresh.Add(()=>{var n=Find(1026);var a=Find(1025);string nt=n==null?"":Native.Text(n.H).Trim();string at=a==null?"":Native.Text(a.H).Trim();
            var lines=at.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(l=>l.Trim()).Where(l=>l!="").ToList();
            string uri=lines.Count>0?lines[0]:"";int lt=uri.LastIndexOf('<');if(lt>=0)uri=uri.Substring(lt+1).TrimEnd('>');
            int sign=uri.IndexOf('@');string user=sign>0?uri.Substring(0,sign):uri;string display=nt!=""?nt:user;
            string diversion=lines.FirstOrDefault(l=>l.StartsWith(Host.Translate("Diversion")+":"));
            var extra=new List<string>();if(user!=""&&user!=display)extra.Add(user);if(diversion!=null)extra.Add(diversion);
            name.Text=display;name.ToolTip=at==""?null:at;address.Text=String.Join("\n",extra);address.Visibility=address.Text==""?Visibility.Collapsed:Visibility.Visible;});
        var actions=new UniformGrid{Columns=2};
        foreach(var spec in new[]{new object[]{1000,"Απάντηση",Color.FromRgb(21,128,61)},new object[]{1032,"Απόρριψη",Color.FromRgb(220,38,38)}}){
            var c=Find((int)spec[0],"Button");if(c==null)continue;
            var b=Button((string)spec[1],()=>Native.Value(c.H,0xf5));var fill=new SolidColorBrush((Color)spec[2]);fill.Freeze();
            b.Background=fill;b.BorderBrush=fill;b.Foreground=Brushes.White;b.FontSize=16;b.FontWeight=FontWeights.SemiBold;b.Height=44;b.Margin=new Thickness(4);
            if((int)spec[0]==1000)b.IsDefault=true;
            refresh.Add(()=>{b.IsEnabled=Native.IsWindowEnabled(c.H);b.Visibility=Visible(c.H)?Visibility.Visible:Visibility.Collapsed;});actions.Children.Add(b);
        }
        panel.Children.Add(actions);
        var transfer=Find(1135,"Button");if(transfer!=null){var t=BoundButton(transfer);t.Content="Προώθηση";t.ToolTip="Προώθηση της κλήσης σε άλλο αριθμό χωρίς απάντηση";t.Margin=new Thickness(4,4,4,0);panel.Children.Add(t);}
        body.Children.Add(panel);
    }
    // Transfer / forward (113): number, 3x4 keypad and the visible action buttons, without scrolling.
    void BuildTransfer(){
        var layout=new DockPanel();body.Children.Add(layout);
        var title=Label(Native.Text(parent),19);DockPanel.SetDock(title,Dock.Top);layout.Children.Add(title);
        var actions=new UniformGrid{Columns=2,Margin=new Thickness(0,8,0,0)};DockPanel.SetDock(actions,Dock.Bottom);layout.Children.Add(actions);
        foreach(int id in new[]{1081,1082,1,2}){var c=Find(id,"Button");if(c==null)continue;var b=BoundButton(c);b.Margin=new Thickness(3);b.Height=38;
            refresh.Add(()=>{b.Visibility=Visible(c.H)?Visibility.Visible:Visibility.Collapsed;b.IsEnabled=Native.IsWindowEnabled(c.H);});actions.Children.Add(b);}
        var panel=new StackPanel();layout.Children.Add(panel);
        var number=Find(1078,"ComboBox");
        if(number!=null){var edit=TextField(number);edit.FontSize=21;edit.Margin=new Thickness(0,0,0,8);System.Windows.Automation.AutomationProperties.SetName(edit,"Αριθμός");
            edit.KeyDown+=(s,e)=>{if(e.Key!=Key.Enter)return;foreach(int id in new[]{1,1082}){var c=Find(id,"Button");if(c!=null&&Visible(c.H)&&Native.IsWindowEnabled(c.H)){Native.Value(c.H,0xf5);e.Handled=true;return;}}};
            panel.Children.Add(edit);root.Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=>edit.Focus()));}
        var keys=new UniformGrid{Columns=3,Rows=4};string[] digits={"1","2","3","4","5","6","7","8","9","*","0","#"};
        foreach(string digit in digits){int id=digit=="*"?1072:digit=="#"?1070:1060+int.Parse(digit);var c=Find(id,"Button");if(c==null)continue;
            var b=Button(digit,()=>Native.Value(c.H,0xf5));b.FontSize=18;b.Height=40;b.Padding=new Thickness(2);b.Margin=new Thickness(3);System.Windows.Automation.AutomationProperties.SetName(b,digit);keys.Children.Add(b);}
        panel.Children.Add(keys);
    }
    void Phone(Panel panel){
        var number=Find(1078,"ComboBox");panel.Children.Add(Label("Αριθμός τηλεφώνου",12));if(number!=null){var edit=TextField(number);edit.FontSize=25;edit.Margin=new Thickness(0,0,0,12);
            edit.KeyDown+=(s,e)=>{if(e.Key==Key.Enter){var call=Find(1022,"Button");if(call!=null&&Visible(call.H)&&Native.IsWindowEnabled(call.H)){Native.Value(call.H,0xf5);e.Handled=true;}}};
            edit.PreviewTextInput+=(s,e)=>{var end=Find(1055,"Button");if(end==null||!Visible(end.H)||e.Text.Length!=1)return;char ch=e.Text[0];int key=ch=='*'?1072:ch=='#'?1070:ch>='0'&&ch<='9'?1060+(ch-'0'):0;var c=key==0?null:Find(key,"Button");if(c!=null){Native.Value(c.H,0xf5);e.Handled=true;}};
            panel.Children.Add(edit);}
        var keys=new UniformGrid{Columns=3,Rows=4};string[] digits={"1","2","3","4","5","6","7","8","9","*","0","#"};string[] letters={"","ABC","DEF","GHI","JKL","MNO","PQRS","TUV","WXYZ","","+",""};
        for(int i=0;i<12;i++){string digit=digits[i];int id=digit=="*"?1072:digit=="#"?1070:1060+int.Parse(digit);var c=Find(id,"Button");var b=Button(digit,()=>{if(c!=null)Native.Value(c.H,0xf5);});var p=new StackPanel();var d=Label(digit,21);d.TextAlignment=TextAlignment.Center;d.Margin=new Thickness(0);d.LineHeight=24;d.LineStackingStrategy=LineStackingStrategy.BlockLineHeight;p.Children.Add(d);var l=Label(letters[i],10);l.TextAlignment=TextAlignment.Center;l.SetResourceReference(TextBlock.ForegroundProperty,"Muted");l.Margin=new Thickness(0,1,0,0);l.LineHeight=13;l.LineStackingStrategy=LineStackingStrategy.BlockLineHeight;p.Children.Add(l);p.VerticalAlignment=VerticalAlignment.Center;b.Content=p;b.Height=52;b.Padding=new Thickness(4,2,4,2);b.Margin=new Thickness(4);System.Windows.Automation.AutomationProperties.SetName(b,digit);keys.Children.Add(b);}panel.Children.Add(keys);
        var aux=new UniformGrid{Columns=3,Margin=new Thickness(0,6,0,8)};foreach(int id in new[]{1200,1071,1028}){var c=Find(id,"Button");if(c!=null){var b=BoundButton(c);b.Margin=new Thickness(3);b.Padding=new Thickness(2,8,2,8);b.FontSize=12;ShrinkToFit(b);aux.Children.Add(b);}}panel.Children.Add(aux);
        var dial=new Grid{Margin=new Thickness(0,0,0,8)};dial.ColumnDefinitions.Add(new ColumnDefinition());dial.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});foreach(int id in new[]{1022,1055}){var c=Find(id,"Button");if(c!=null){var b=BoundButton(c);if(id==1055){b.Background=new SolidColorBrush(Color.FromRgb(220,38,38));b.Foreground=Brushes.White;}dial.Children.Add(b);}}var message=Find(1076,"Button");if(message!=null){var b=BoundButton(message);b.Content="Μήνυμα";b.Margin=new Thickness(6,4,0,4);Grid.SetColumn(b,1);dial.Children.Add(b);}panel.Children.Add(dial);
        var active=new WrapPanel();foreach(int id in new[]{1057,1135,1012}){var c=Find(id,"Button");if(c!=null){var f=Field(c);f.Margin=new Thickness(0,4,8,4);active.Children.Add(f);}}panel.Children.Add(active);
        var levels=new Grid{Margin=new Thickness(0,4,0,4)};levels.ColumnDefinitions.Add(new ColumnDefinition());levels.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(16)});levels.ColumnDefinitions.Add(new ColumnDefinition());int column=0;
        foreach(var spec in new[]{new object[]{1019,1144,"\uE767","Σίγαση ηχείου"},new object[]{1018,1143,"\uE720","Σίγαση μικροφώνου"}}){
            var row=new DockPanel();var check=Find((int)spec[0],"Button");var slider=Find((int)spec[1],"msctls_trackbar32");
            if(check!=null){var f=Field(check);var box=f as CheckBox;if(box!=null){var icon=new TextBlock{Text=(string)spec[2],FontFamily=new FontFamily("Segoe MDL2 Assets"),FontSize=15,VerticalAlignment=VerticalAlignment.Center};icon.SetResourceReference(TextBlock.ForegroundProperty,"Ink");box.Content=icon;box.ToolTip=spec[3];System.Windows.Automation.AutomationProperties.SetName(box,(string)spec[3]);}DockPanel.SetDock(f,Dock.Left);f.Margin=new Thickness(0,0,6,0);row.Children.Add(f);}
            if(slider!=null)row.Children.Add(Field(slider));
            Grid.SetColumn(row,column);levels.Children.Add(row);column+=2;
        }
        panel.Children.Add(levels);
        var modes=new UniformGrid{Rows=1,Margin=new Thickness(0,6,0,0)};IntPtr dialer=number==null?IntPtr.Zero:Native.GetParent(number.H);foreach(int id in new[]{1001,1002,1005,1011}){var c=controls.FirstOrDefault(x=>x.Id==id&&x.Class=="Button"&&Native.GetParent(x.H)==dialer);if(c!=null){var b=BoundButton(c);b.Margin=new Thickness(3);b.Padding=new Thickness(2,9,2,9);b.FontSize=12;ShrinkToFit(b);modes.Children.Add(b);}}panel.Children.Add(modes);
    }
    // Contacts (1030) and call history (1027). The native list keeps the real selection;
    // the WPF list mirrors it, so Call/Edit/Delete always act on the highlighted row.
    void Listing(Panel panel,int id){
        var list=Find(id,"SysListView32");var search=controls.FirstOrDefault(c=>c.Class=="Edit"&&list!=null&&Native.GetParent(c.H)==Native.GetParent(list.H));
        bool fill=panel is DockPanel;
        if(search!=null){var searchLabel=Label("Αναζήτηση",12);var searchField=TextField(search);DockPanel.SetDock(searchLabel,Dock.Top);DockPanel.SetDock(searchField,Dock.Top);panel.Children.Add(searchLabel);panel.Children.Add(searchField);}
        if(list==null)return;
        bool contacts=id==1030;IntPtr owner=Native.GetParent(list.H);
        Func<int> selectedRow=()=>Native.SendMessage(list.H,0x100c,new IntPtr(-1),new IntPtr(2)).ToInt32();
        Action<int> command=cmd=>Native.Command(owner,cmd);
        var lb=new ListBox{MinHeight=fill?0:300,Margin=new Thickness(0,12,0,0),ItemTemplate=ListRow.Template(),HorizontalContentAlignment=HorizontalAlignment.Stretch,SelectionMode=SelectionMode.Extended};
        TextSearch.SetTextPath(lb,"Title");
        System.Windows.Automation.AutomationProperties.SetName(lb,contacts?"Επαφές":"Ιστορικό κλήσεων");
        string previous="";int lastCount=-1;int ticks=0;
        refresh.Add(()=>{
            int count=Native.Value(list.H,0x1004);
            // Full re-read when the row count changes, otherwise about once a second.
            if(count!=lastCount||++ticks%6==0){
                lastCount=count;
                var items=new List<ListRow>();for(int row=0;row<count;row++)items.Add(new ListRow(Enumerable.Range(0,3).Select(col=>Native.ListCell(list.H,row,col)).ToArray()));
                string signature=String.Join("\n",items);if(signature!=previous){previous=signature;lb.ItemsSource=items;}
            }
            // A multiple selection made here is kept; single selections follow the native list.
            int sel=selectedRow();if(Native.Value(list.H,0x1032)<=1&&lb.SelectedItems.Count<=1&&lb.SelectedIndex!=sel)lb.SelectedIndex=sel;
        });
        lb.SelectionChanged+=(s,e)=>{if(syncing||lb.SelectedIndex<0)return;
            if(lb.SelectedItems.Count<=1)Native.ListSelect(list.H,lb.SelectedIndex);
            else Native.ListSelectMany(list.H,lb.SelectedItems.Cast<object>().Select(o=>lb.Items.IndexOf(o)).Where(i=>i>=0).ToList(),lb.SelectedIndex);};
        lb.MouseDoubleClick+=(s,e)=>{if(lb.SelectedIndex>=0)Native.ListActivate(list.H,lb.SelectedIndex);};
        lb.KeyDown+=(s,e)=>{
            if(lb.SelectedIndex<0)return;
            if(e.Key==Key.Enter){Native.ListActivate(list.H,lb.SelectedIndex);e.Handled=true;}
            else if(e.Key==Key.Delete){command(32782);e.Handled=true;}
        };
        // Right click selects the row under the pointer before the menu opens.
        lb.PreviewMouseRightButtonDown+=(s,e)=>{var item=ItemsControl.ContainerFromElement(lb,e.OriginalSource as DependencyObject) as ListBoxItem;if(item!=null)item.IsSelected=true;};
        var menu=new ContextMenu();menu.Resources.MergedDictionaries.Add(Host.Theme);
        Action<string,int,bool> add=(text,cmd,needsRow)=>{var mi=new MenuItem{Header=text};mi.Click+=(s,e)=>command(cmd);menu.Opened+=(s,e)=>mi.IsEnabled=!needsRow||selectedRow()>=0;menu.Items.Add(mi);};
        add("Κλήση",32777,true);
        if(contacts){add("Επεξεργασία",32783,true);add("Νέα επαφή",32775,false);}else add("Προσθήκη στις επαφές",32775,true);
        // SMS to the selected row's mobile number (the first cell that looks like a phone number).
        var sms=new MenuItem{Header="Αποστολή SMS…"};menu.Opened+=(s,e)=>sms.IsEnabled=lb.SelectedItem is ListRow;
        sms.Click+=(s,e)=>{var row=lb.SelectedItem as ListRow;if(row==null)return;
            string number=row.Cells.FirstOrDefault(x=>x.Count(char.IsDigit)>=10)??"";
            Sms.Compose(Native.GetAncestor(parent,2),number,contacts&&row.Title!=number?row.Title:"");};
        menu.Items.Add(sms);
        add("Αντιγραφή",32781,true);add("Διαγραφή",32782,true);
        menu.Items.Add(new Separator());
        add("Εξαγωγή…",32804,false);add("Εισαγωγή…",32801,false);
        // The native delete asks for confirmation before removing the selected rows.
        if(contacts){var clearAll=new MenuItem{Header="Διαγραφή όλων των επαφών…"};clearAll.Click+=(s,e)=>{if(Native.Value(list.H,0x1004)==0)return;Native.ListSelectAll(list.H);command(32782);};menu.Items.Add(clearAll);}
        if(!contacts){var clear=new MenuItem{Header="Εκκαθάριση ιστορικού"};clear.Click+=(s,e)=>{if(Native.Value(list.H,0x1004)==0)return;Native.ListSelectAll(list.H);command(32782);};menu.Items.Add(clear);}
        lb.ContextMenu=menu;
        if(!fill)panel.Children.Add(lb);
        var actions=new UniformGrid{Columns=3,Margin=new Thickness(0,8,0,0)};
        var buttons=contacts?new[]{new object[]{"Νέα επαφή",32775,false},new object[]{"Επεξεργασία",32783,true},new object[]{"Κλήση",32777,true}}
                            :new[]{new object[]{"Κλήση",32777,true},new object[]{"Στις επαφές",32775,true},new object[]{"Διαγραφή",32782,true}};
        foreach(var spec in buttons){int cmd=(int)spec[1];bool needsRow=(bool)spec[2];var action=Button((string)spec[0],()=>{if(!needsRow||selectedRow()>=0)command(cmd);},cmd==32777);action.Margin=new Thickness(3);refresh.Add(()=>action.IsEnabled=!needsRow||selectedRow()>=0);actions.Children.Add(action);}
        DockPanel.SetDock(actions,Dock.Bottom);panel.Children.Add(actions);
        if(contacts&&fill)LetterIndex(panel,lb);
        if(fill){
            var area=new Grid();area.Children.Add(lb);
            // Empty contact list: say so and offer the two ways to fill it (import is otherwise only in the context menu).
            if(contacts){
                var empty=new StackPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(20,12,20,0),MaxWidth=280};
                var heading=Label("Δεν υπάρχουν επαφές",15);heading.TextAlignment=TextAlignment.Center;heading.FontWeight=FontWeights.SemiBold;empty.Children.Add(heading);
                var hint=Label("Προσθέστε μία ή φέρτε τις επαφές σας από Google ή Outlook (αρχείο CSV).",12);hint.TextAlignment=TextAlignment.Center;hint.SetResourceReference(TextBlock.ForegroundProperty,"Muted");hint.Margin=new Thickness(0,0,0,14);empty.Children.Add(hint);
                var import=Button("Εισαγωγή από αρχείο…",()=>command(32801),true);import.Margin=new Thickness(0,0,0,6);empty.Children.Add(import);
                var create=Button("Νέα επαφή",()=>command(32775));empty.Children.Add(create);
                refresh.Add(()=>{bool none=Native.Value(list.H,0x1004)==0;bool filtered=search!=null&&Native.Text(search.H).Trim()!="";
                    empty.Visibility=none?Visibility.Visible:Visibility.Collapsed;heading.Text=filtered?"Καμία επαφή δεν ταιριάζει":"Δεν υπάρχουν επαφές";
                    hint.Visibility=import.Visibility=create.Visibility=filtered?Visibility.Collapsed:Visibility.Visible;});
                area.Children.Add(empty);
            }
            else{
                var none=Label("Δεν υπάρχουν κλήσεις ακόμη",14);none.TextAlignment=TextAlignment.Center;none.VerticalAlignment=VerticalAlignment.Center;none.HorizontalAlignment=HorizontalAlignment.Center;
                none.SetResourceReference(TextBlock.ForegroundProperty,"Muted");none.IsHitTestVisible=false;
                refresh.Add(()=>{bool filtered=search!=null&&Native.Text(search.H).Trim()!="";none.Text=filtered?"Καμία κλήση δεν ταιριάζει":"Δεν υπάρχουν κλήσεις ακόμη";
                    none.Visibility=Native.Value(list.H,0x1004)==0?Visibility.Visible:Visibility.Collapsed;});
                area.Children.Add(none);
            }
            panel.Children.Add(area);
        }
    }
    // Contacts: column of the initials present in the list (Greek, then Latin, then #); a click selects the first
    // contact with that initial and brings it to the top of the list.
    void LetterIndex(Panel panel,ListBox lb){
        var letters=new StackPanel();
        var strip=new Viewbox{Child=letters,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(4,12,0,0)};
        DockPanel.SetDock(strip,Dock.Right);panel.Children.Add(strip);
        string shown=null;
        refresh.Add(()=>{
            var items=lb.ItemsSource as List<ListRow>;if(items==null)return;
            var keys=items.Select(x=>Initial(x.Title)).Distinct().OrderBy(k=>k=="#"?2:k[0]>='Α'&&k[0]<='Ω'?0:1).ThenBy(k=>k,StringComparer.Ordinal).ToList();
            string sign=String.Join("",keys);if(sign==shown)return;shown=sign;letters.Children.Clear();
            foreach(string key in keys){
                var letter=new TextBlock{Text=key,FontSize=11,FontWeight=FontWeights.SemiBold,Padding=new Thickness(5,1,5,1),TextAlignment=TextAlignment.Center,Cursor=Cursors.Hand,ToolTip="Μετάβαση στο "+key};
                letter.SetResourceReference(TextBlock.ForegroundProperty,"Primary");
                letter.MouseLeftButtonUp+=(s,e)=>{var current=lb.ItemsSource as List<ListRow>;if(current==null||current.Count==0)return;int index=current.FindIndex(x=>Initial(x.Title)==key);if(index<0)return;
                    lb.SelectedIndex=index;lb.ScrollIntoView(current[current.Count-1]);lb.UpdateLayout();lb.ScrollIntoView(current[index]);e.Handled=true;};
                letters.Children.Add(letter);
            }
        });
    }
    static string Initial(string text){
        text=(text??"").TrimStart();if(text=="")return "#";
        // Emoji and other surrogate pairs cannot be normalized on their own: they go under "#".
        if(Char.IsSurrogate(text[0]))return "#";
        string first=text.Substring(0,1).Normalize(NormalizationForm.FormD);char c=Char.ToUpperInvariant(first[0]);
        return Char.IsLetter(c)?c.ToString():"#";
    }
    void BuildForm(){var layout=new DockPanel();body.Children.Add(layout);var title=Label(dialogId==107?"Λογαριασμός SIP":dialogId==108?"Ρυθμίσεις":Native.Text(parent),21);DockPanel.SetDock(title,Dock.Top);layout.Children.Add(title);var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,14,0,0)};foreach(var c in controls.Where(c=>c.Class=="Button"&&(c.Id==1||c.Id==2))){var b=BoundButton(c);b.Margin=new Thickness(6,0,0,0);b.MinWidth=100;buttons.Children.Add(b);}DockPanel.SetDock(buttons,Dock.Bottom);layout.Children.Add(buttons);var content=new StackPanel();var scroll=new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};if(dialogId==108){var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(165)});grid.ColumnDefinitions.Add(new ColumnDefinition());var sidebar=new StackPanel{Margin=new Thickness(0,0,20,0)};string[] sections={"Ήχος","Κλήσεις","Δίκτυο","Γενικά","Εμφάνιση"};if(currentSection=="")currentSection=sections[0];foreach(string section in sections){var b=Button(section,()=>{currentSection=section;Build();},currentSection==section);b.HorizontalContentAlignment=HorizontalAlignment.Left;sidebar.Children.Add(b);}grid.Children.Add(sidebar);Grid.SetColumn(scroll,1);grid.Children.Add(scroll);layout.Children.Add(grid);if(currentSection=="Εμφάνιση"){content.Children.Add(Label("Θέμα εφαρμογής",17));content.Children.Add(Label("Η επιλογή εφαρμόζεται σε όλα τα παράθυρα.",13));foreach(var pair in new[]{new[]{"Φωτεινό","light"},new[]{"Σκοτεινό","dark"},new[]{"Αυτόματο βάσει Windows","system"}})content.Children.Add(Button(pair[0],()=>Host.SetMode(pair[1]),Host.Mode==pair[1]));return;}}else layout.Children.Add(scroll);
        var active=controls.Where(c=>Visible(c.H)&&(c.Input||c.Class=="Button"||c.Class=="SysLink")&&c.Id!=1&&c.Id!=2&&!(c.Class=="SysLink"&&Clean(c.Caption)=="?")).OrderBy(c=>c.Rect.Top).ThenBy(c=>c.Rect.Left).ToList();
        if(dialogId==108)active=active.Where(c=>Section(c)==currentSection).ToList();
        // Account (107): label, server, username, domain, password (+show), display name.
        var basicAccount=new[]{1154,1152,1052,1053,1046,1049,1109,1045};
        var advanced=new StackPanel{Visibility=AdvancedOpen?Visibility.Visible:Visibility.Collapsed};
        foreach(var c in active){
            if(dialogId==108&&new[]{1004,1005,1063,1064}.Contains(c.Id))continue;
            var field=Field(c);
            if(dialogId==108&&(c.Id==1003||c.Id==1062)){
                var pathRow=new DockPanel();int browse=c.Id==1003?1004:1063;int reset=c.Id==1003?1005:1064;
                foreach(int action in new[]{reset,browse}){var original=Find(action,"Button");if(original!=null){var button=BoundButton(original);button.Content=action==browse?"Επιλογή…":"Επαναφορά";refresh.Add(()=>button.Content=action==browse?"Επιλογή…":"Επαναφορά");button.Margin=new Thickness(8,0,0,0);DockPanel.SetDock(button,Dock.Right);pathRow.Children.Add(button);}}
                pathRow.Children.Add(field);field=pathRow;
                if(c.Id==1003){
                    // Suggested ringtones shipped in ringtones\ (generated by scripts/generate-sounds.py).
                    var tones=new[]{new[]{"netmind (προεπιλογή)",""},new[]{"Κλασικό τηλέφωνο","ringtones\\classic.wav"},new[]{"Παλιό τηλέφωνο","ringtones\\oldbell.wav"},new[]{"Τηλέφωνο γραφείου","ringtones\\office.wav"},new[]{"Ψηφιακό","ringtones\\digital.wav"},new[]{"Απαλό (πιάνο)","ringtones\\soft.wav"},new[]{"Μαρίμπα","ringtones\\marimba.wav"},new[]{"Διακριτικό","ringtones\\discreet.wav"}};
                    var picker=new ComboBox{Margin=new Thickness(0,0,0,8)};foreach(var t in tones)picker.Items.Add(t[0]);picker.Items.Add("Άλλο αρχείο WAV…");
                    System.Windows.Automation.AutomationProperties.SetName(picker,"Ήχος κλήσης");
                    var native=c;var custom=pathRow;bool updating=false;
                    Action sync=()=>{string cur=Native.Text(native.H).Trim();int idx=Array.FindIndex(tones,t=>String.Equals(t[1],cur,StringComparison.OrdinalIgnoreCase));if(idx<0)idx=tones.Length;
                        updating=true;if(!picker.IsDropDownOpen&&picker.SelectedIndex!=idx)picker.SelectedIndex=idx;updating=false;custom.Visibility=idx==tones.Length?Visibility.Visible:Visibility.Collapsed;};
                    sync();refresh.Add(sync);
                    picker.SelectionChanged+=(sender,args)=>{if(updating||syncing||picker.SelectedIndex<0)return;
                        if(picker.SelectedIndex<tones.Length)Write(native,tones[picker.SelectedIndex][1]);
                        else{var browseButton=Find(1004,"Button");if(browseButton!=null)Native.Value(browseButton.H,0xf5);}};
                    var choice=new StackPanel();choice.Children.Add(picker);choice.Children.Add(pathRow);field=choice;
                }
            }if(field==null)continue;var row=new StackPanel{Margin=new Thickness(0,0,4,9)};string label=Override(c)??LabelFor(c);if(label!=""&&c.Class!="Button"&&c.Class!="SysLink"){var l=Label(label,12);l.SetResourceReference(TextBlock.ForegroundProperty,"Muted");l.Margin=new Thickness(0,0,0,3);row.Children.Add(l);}row.Children.Add(field);
            // A chosen provider fills server and domain, so they are not shown in the basic view.
            // With a provider already chosen they move to the advanced section; if one is chosen while the form is open, they hide.
            Func<bool> providerChosen=()=>{var provider=Find(1154,"ComboBox");return provider!=null&&Native.Value(provider.H,0x147)>0;};
            bool presetField=dialogId==107&&(c.Id==1052||c.Id==1046),toAdvanced=presetField&&providerChosen();
            if(presetField&&!toAdvanced){var presetRow=row;refresh.Add(()=>presetRow.Visibility=providerChosen()?Visibility.Collapsed:Visibility.Visible);}
            (dialogId==107&&(!basicAccount.Contains(c.Id)||toAdvanced)?advanced:content).Children.Add(row);}
        if(advanced.Children.Count>0){
            var toggle=Button("",()=>{});toggle.HorizontalAlignment=HorizontalAlignment.Left;toggle.Margin=new Thickness(0,4,0,10);
            Action label=()=>toggle.Content=(AdvancedOpen?"\u25BE  ":"\u25B8  ")+"Για προχωρημένους";label();
            toggle.Click+=(s,e)=>{AdvancedOpen=!AdvancedOpen;advanced.Visibility=AdvancedOpen?Visibility.Visible:Visibility.Collapsed;label();};
            content.Children.Add(toggle);content.Children.Add(advanced);
        }
    }
    static bool AdvancedOpen;
    // Settings (108): fields whose nearest native label is wrong or missing in the compact MicroSIP layout.
    static readonly Dictionary<int,string> SettingsLabels=new Dictionary<int,string>{
        {1062,"Φάκελος ηχογραφήσεων"},{1061,"Αυτόματη ηχογράφηση όλων των κλήσεων"},{1081,"Κουμπί ηχογράφησης στην κλήση"},
        {1076,"Επιλογές αυτόματης απάντησης…"},{1091,"Αριθμός προώθησης"},{1092,"Καθυστέρηση προώθησης (δευτερόλεπτα)"},
        {1044,"Θύρα SIP (0 = αυτόματα)"},{1045,"Θύρες ήχου RTP: από (0 = αυτόματα)"},{1046,"Θύρες ήχου RTP: έως"},
        {1049,"Διακομιστές DNS"},{1031,"Χρήση διακομιστή STUN"},{1032,"Διακομιστής STUN"}};
    string Override(ControlInfo c){string s;return dialogId==108&&SettingsLabels.TryGetValue(c.Id,out s)?s:null;}
    string Section(ControlInfo c){int id=c.Id;if(id>=2000)return "Γενικά";if(new[]{1003,1004,1005,1006,1007,1008,1009,1010,1011,1012,1013,1014,1015,1016,1017,1025,1075}.Contains(id))return "Ήχος";if(new[]{1031,1032,1043,1044,1045,1046,1049,1050,1112}.Contains(id))return "Δίκτυο";if(new[]{1027,1028,1029,1036,1061,1062,1063,1064,1076,1077,1079,1080,1081,1090,1091,1092,1110}.Contains(id))return "Κλήσεις";return "Γενικά";}
    static string Clean(string s){return Host.Translate(System.Text.RegularExpressions.Regex.Replace(s,"<[^>]*>","").Replace("&","").Trim());}
    string LabelFor(ControlInfo c){if(c.Class=="msctls_trackbar32"&&c.Caption!="")return Clean(c.Caption);var labels=controls.Where(x=>x.Class=="Static"&&Clean(x.Caption)!=""&&Clean(x.Caption)!="*"&&x.Rect.Right>x.Rect.Left);var best=labels.Where(x=>x.Rect.Right<=c.Rect.Left+4&&Math.Abs(x.Rect.Top-c.Rect.Top)<15).OrderBy(x=>c.Rect.Left-x.Rect.Right).FirstOrDefault();if(best==null)best=labels.Where(x=>x.Rect.Top<c.Rect.Top&&c.Rect.Top-x.Rect.Bottom<25&&Math.Abs(x.Rect.Left-c.Rect.Left)<35).OrderBy(x=>c.Rect.Top-x.Rect.Bottom).FirstOrDefault();return best==null?(c.Class=="ListBox"?"Επιλογές":""):Clean(best.Caption);}
    FrameworkElement Field(ControlInfo c){FrameworkElement field=null;if(c.Class=="Button"){int type=c.Style&15;if(type==2||type==3||type==4||type==5||type==6||type==9){var check=new CheckBox{Content=Override(c)??Clean(c.Caption),IsChecked=Native.Value(c.H,0xf0)!=0};check.Click+=(s,e)=>{if(!syncing)Native.Value(c.H,0xf5);};refresh.Add(()=>check.IsChecked=Native.Value(c.H,0xf0)!=0);field=check;}else field=BoundButton(c);}else if(c.Class=="Edit"||c.Class.StartsWith("RichEdit",StringComparison.OrdinalIgnoreCase)||c.Class=="ComboBox"&&(c.Style&3)!=3){if((c.Style&0x20)!=0&&c.Class=="Edit"){var password=new PasswordBox{Password=Native.Text(c.H)};password.PasswordChanged+=(s,e)=>{if(!syncing)Write(c,password.Password);};refresh.Add(()=>{if(!password.IsKeyboardFocusWithin&&password.Password!=Native.Text(c.H))password.Password=Native.Text(c.H);});field=password;}else field=TextField(c);}else if(c.Class=="ComboBox"){var combo=new ComboBox();string signature="";refresh.Add(()=>{int count=Native.Value(c.H,0x146);var items=Enumerable.Range(0,Math.Max(0,count)).Select(i=>Native.Item(c.H,0x148,i)).ToArray();string sign=String.Join("\n",items);if(signature!=sign){signature=sign;combo.ItemsSource=items;}int n=Native.Value(c.H,0x147);if(!combo.IsDropDownOpen&&combo.SelectedIndex!=n)combo.SelectedIndex=n;});combo.SelectionChanged+=(s,e)=>{if(!syncing&&combo.SelectedIndex>=0){Native.SendMessage(c.H,0x14e,new IntPtr(combo.SelectedIndex),IntPtr.Zero);Native.Changed(c.H,1);}};field=combo;}else if(c.Class=="ListBox"){var list=new ListBox{MinHeight=120,MaxHeight=200};string signature="";refresh.Add(()=>{int count=Native.Value(c.H,0x18b);string[] items=Enumerable.Range(0,Math.Max(0,count)).Select(i=>Native.Item(c.H,0x189,i)).ToArray();string sign=String.Join("\n",items);if(signature!=sign){signature=sign;list.ItemsSource=items;}int n=Native.Value(c.H,0x188);if(list.SelectedIndex!=n)list.SelectedIndex=n;});list.SelectionChanged+=(s,e)=>{if(!syncing)Native.SendMessage(c.H,0x186,new IntPtr(list.SelectedIndex),IntPtr.Zero);};list.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Space||e.Key==Key.Delete){Native.SendMessage(Native.GetParent(c.H),0x2e,new IntPtr((list.SelectedIndex<<16)|(e.Key==Key.Space?32:46)),c.H);e.Handled=true;}};var wrap=new StackPanel();wrap.Children.Add(list);if(dialogId==108&&(c.Id==1011||c.Id==1012)){wrap.Children.Add(Button(c.Id==1011?"Ενεργοποίηση επιλεγμένου":"Απενεργοποίηση επιλεγμένου",()=>Native.SendMessage(parent,0x2e,new IntPtr((list.SelectedIndex<<16)|(c.Id==1011?32:46)),c.H)));}field=wrap;}else if(c.Class=="msctls_trackbar32"){var slider=new Slider{Minimum=Native.Value(c.H,0x401),Maximum=Native.Value(c.H,0x402),Value=Native.Value(c.H,0x400),Margin=new Thickness(0,5,0,5)};slider.ValueChanged+=(s,e)=>{if(!syncing){Native.SendMessage(c.H,0x405,new IntPtr(1),new IntPtr((int)slider.Value));Native.SendMessage(Native.GetParent(c.H),0x114,new IntPtr(5),c.H);}};slider.AddHandler(Thumb.DragCompletedEvent,new DragCompletedEventHandler((s,e)=>Native.SendMessage(Native.GetParent(c.H),0x114,new IntPtr(8),c.H)));refresh.Add(()=>{if(!slider.IsKeyboardFocusWithin&&!slider.IsMouseCaptureWithin)slider.Value=Native.Value(c.H,0x400);});field=slider;}else if(c.Class=="SysLink"){var b=Button(Clean(c.Caption),()=>Native.Notify(c.H,-2));field=b;}else if(c.Class=="msctls_updown32"){var buttons=new WrapPanel();buttons.Children.Add(Button(c.Id==1013?"Μεταφορά δεξιά":"Μετακίνηση πάνω",()=>Native.Spin(c.H,-1)));buttons.Children.Add(Button(c.Id==1013?"Μεταφορά αριστερά":"Μετακίνηση κάτω",()=>Native.Spin(c.H,1)));field=buttons;}else if(c.Class=="SysListView32"){var p=new StackPanel();Listing(p,c.Id);field=p;}
        if(field!=null){field.Tag=c.H;System.Windows.Automation.AutomationProperties.SetAutomationId(field,"native-"+c.Id);refresh.Add(()=>{field.IsEnabled=Native.IsWindowEnabled(c.H);field.Visibility=Visible(c.H)?Visibility.Visible:Visibility.Collapsed;});}return field;
    }
    // Button text shrinks instead of being cut when the window is narrow (the content stays a plain string).
    static void ShrinkToFit(Button b){
        var box=new FrameworkElementFactory(typeof(Viewbox));box.SetValue(Viewbox.StretchDirectionProperty,StretchDirection.DownOnly);
        var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding());
        box.AppendChild(text);b.ContentTemplate=new DataTemplate{VisualTree=box};
    }
    Button BoundButton(ControlInfo c){
        var b=Button(Override(c)??Clean(c.Caption),()=>Native.Value(c.H,0xf5),c.Id==1||c.Id==1022);
        int type=c.Style&15;bool toggle=((c.Style&0x1000)!=0&&(type==2||type==3||type==5||type==6))||(type==0xb&&new[]{1001,1002,1005,1011}.Contains(c.Id));
        refresh.Add(()=>{
            b.IsEnabled=Native.IsWindowEnabled(c.H);b.Visibility=Visible(c.H)?Visibility.Visible:Visibility.Collapsed;
            string text=Override(c)??Clean(Native.Text(c.H));
            if(toggle){bool active=Native.Value(c.H,0xf0)!=0;string state=active?"Ενεργό":"Ανενεργό";
                b.SetResourceReference(Control.BackgroundProperty,active?"Tint":"Paper");
                if(active)b.BorderBrush=new SolidColorBrush(Color.FromRgb(59,130,246));else b.SetResourceReference(Control.BorderBrushProperty,"Line");
                b.SetResourceReference(Control.ForegroundProperty,"Ink");
                b.ToolTip=text+": "+state;System.Windows.Automation.AutomationProperties.SetHelpText(b,state);
                text=(active?"✓  ":"")+text;
            }
            if(text!=""&&b.Content as string!=text)b.Content=text;
        });return b;
    }
    TextBox TextField(ControlInfo c){var edit=new TextBox{Text=Native.Text(c.H),IsReadOnly=(c.Style&0x800)!=0,AcceptsReturn=(c.Style&4)!=0};if(edit.AcceptsReturn){edit.MinHeight=80;edit.MaxHeight=220;edit.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;}edit.TextChanged+=(s,e)=>{if(!syncing)Write(c,edit.Text);};refresh.Add(()=>{edit.IsEnabled=Native.IsWindowEnabled(c.H);if(!edit.IsKeyboardFocusWithin){string text=Native.Text(c.H);if(edit.Text!=text)edit.Text=text;}});return edit;}
    void Write(ControlInfo c,string text){if(!Native.Own(c.H))return;Native.SendText(c.H,0x0c,IntPtr.Zero,text);if(c.Class=="ComboBox")Native.Changed(c.H,5);}
    public void Dispose(){if(disposed)return;disposed=true;timer.Stop();source.RootVisual=null;source.Dispose();refresh.Clear();}
}
}
