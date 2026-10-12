#pragma once
#include "NetmindTheme.h"
class CNetmindTabs : public CTabCtrl {
protected:
 LRESULT WindowProc(UINT msg,WPARAM wp,LPARAM lp) override {
  if(msg==WM_ERASEBKGND)return TRUE;
  if(msg==WM_PAINT){CPaintDC dc(this);CRect all;GetClientRect(all);dc.FillSolidRect(all,NM_CANVAS);dc.SetBkMode(TRANSPARENT);CFont* old=dc.SelectObject(GetFont());
   for(int i=0;i<GetItemCount();i++){CRect rect;GetItemRect(i,rect);rect.DeflateRect(2,2);bool chosen=i==GetCurSel();NetmindRounded(dc,rect,chosen?NM_TINT:NM_CANVAS,chosen?NM_TINT:NM_CANVAS,10);
    TCHAR label[128]={0};TCITEM item={0};item.mask=TCIF_TEXT;item.pszText=label;item.cchTextMax=128;GetItem(i,&item);dc.SetTextColor(chosen?NM_BLUE_DARK:NM_MUTED);dc.DrawText(label,rect,DT_CENTER|DT_VCENTER|DT_SINGLELINE);
    if(chosen && GetFocus()==this){rect.DeflateRect(4,4);dc.DrawFocusRect(rect);}}
   dc.SelectObject(old);return 0;}
  return CTabCtrl::WindowProc(msg,wp,lp);
 }
};
