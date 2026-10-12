// netmind UI, 2026-10-12. Shared colors and private font resources.
#pragma once
#include <afxwin.h>
const COLORREF NM_CANVAS = RGB(247,249,252);
const COLORREF NM_PAPER = RGB(255,255,255);
const COLORREF NM_BLUE = RGB(59,130,246);
const COLORREF NM_BLUE_DARK = RGB(37,99,235);
const COLORREF NM_TINT = RGB(239,245,255);
const COLORREF NM_INK = RGB(20,20,20);
const COLORREF NM_MUTED = RGB(107,114,128);
const COLORREF NM_LINE = RGB(229,231,235);
inline void NetmindLoadFonts() {
    static bool loaded=false;
    if(loaded)return;
    for(int id=6000;id<=6002;id++) {
        HRSRC res=FindResource(AfxGetInstanceHandle(),MAKEINTRESOURCE(id),RT_RCDATA);
        if(res) { HGLOBAL data=LoadResource(AfxGetInstanceHandle(),res);DWORD count=0;
            AddFontMemResourceEx(LockResource(data),SizeofResource(AfxGetInstanceHandle(),res),NULL,&count);
        }
    }
    loaded=true;
}
inline void NetmindRounded(CDC& dc,CRect rect,COLORREF fill,COLORREF line,int radius=10,int width=1) {
    CBrush brush(fill);CPen pen(PS_SOLID,width,line);
    CBrush* oldBrush=dc.SelectObject(&brush);CPen* oldPen=dc.SelectObject(&pen);
    dc.RoundRect(rect,CPoint(radius,radius));
    dc.SelectObject(oldBrush);dc.SelectObject(oldPen);
}
