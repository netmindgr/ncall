// netmind modern UI bridge, 2026. GPL-3.0-or-later.
// CLR ABI follows Microsoft's public ICLRRuntimeHost interface.
#pragma once
#include <unknwn.h>
struct NmClrHost : IUnknown {
    virtual HRESULT STDMETHODCALLTYPE Start()=0;
    virtual HRESULT STDMETHODCALLTYPE Stop()=0;
    virtual HRESULT STDMETHODCALLTYPE SetHostControl(void*)=0;
    virtual HRESULT STDMETHODCALLTYPE GetCLRControl(void**)=0;
    virtual HRESULT STDMETHODCALLTYPE UnloadAppDomain(DWORD,BOOL)=0;
    virtual HRESULT STDMETHODCALLTYPE ExecuteInAppDomain(DWORD,void*,void*)=0;
    virtual HRESULT STDMETHODCALLTYPE GetCurrentAppDomainId(DWORD*)=0;
    virtual HRESULT STDMETHODCALLTYPE ExecuteApplication(LPCWSTR,DWORD,LPCWSTR*,DWORD,LPCWSTR*,int*)=0;
    virtual HRESULT STDMETHODCALLTYPE ExecuteInDefaultAppDomain(LPCWSTR,LPCWSTR,LPCWSTR,LPCWSTR,DWORD*)=0;
};
inline int NetmindUI(LPCWSTR method, LPCWSTR argument) {
    static NmClrHost* runtime=nullptr;
    static bool attempted=false;
    static CString assembly;
    if(!attempted) {
        attempted=true;
        wchar_t path[32768];
        if(!GetModuleFileNameW(nullptr,path,_countof(path))) return -1;
        assembly=path; assembly=assembly.Left(assembly.ReverseFind(L'\\')+1)+L"netmind.ui.dll";
        if(GetFileAttributesW(assembly)==INVALID_FILE_ATTRIBUTES) return -1;
        HMODULE shim=LoadLibraryExW(L"mscoree.dll",nullptr,LOAD_LIBRARY_SEARCH_SYSTEM32);
        if(!shim) return -1;
        typedef HRESULT (STDAPICALLTYPE* Bind)(LPCWSTR,LPCWSTR,DWORD,REFCLSID,REFIID,void**);
        Bind bind=reinterpret_cast<Bind>(GetProcAddress(shim,"CorBindToRuntimeEx"));
        const GUID clsid={0x90f1a06e,0x7712,0x4762,{0x86,0xb5,0x7a,0x5e,0xba,0x6b,0xdb,0x02}};
        const GUID iid={0x90f1a06c,0x7712,0x4762,{0x86,0xb5,0x7a,0x5e,0xba,0x6b,0xdb,0x02}};
        if(!bind || FAILED(bind(L"v4.0.30319",L"wks",0,clsid,iid,reinterpret_cast<void**>(&runtime)))) {runtime=nullptr;return -1;}
        if(FAILED(runtime->Start())) {runtime->Release();runtime=nullptr;return -1;}
    }
    if(!runtime) return -1;
    DWORD result=0;
    HRESULT hr=runtime->ExecuteInDefaultAppDomain(assembly,L"Netmind.UI.Host",method,argument,&result);
    return SUCCEEDED(hr)?static_cast<int>(result):-1;
}
// Signed, so handles with the high bit set fit IntPtr(long) on 32-bit .NET.
inline CString NetmindHandle(HWND h) {CString arg;arg.Format(L"%lld",static_cast<long long>(reinterpret_cast<INT_PTR>(h)));return arg;}
inline bool NetmindOwnsFocus() {
    HWND focus=GetFocus();wchar_t cls[96]={};
    if(!focus) return false;
    GetClassNameW(focus,cls,_countof(cls));
    return wcsncmp(cls,L"HwndWrapper[",12)==0;
}
