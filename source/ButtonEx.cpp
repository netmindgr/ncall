/*
 * Copyright (C) 2011-2026 MicroSIP (http://www.microsip.org)
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 */

#include "StdAfx.h"   
#include "ButtonEx.h"
#include "NetmindTheme.h"   

// CButtonEx   
IMPLEMENT_DYNAMIC(CButtonEx, CMFCButton)
CButtonEx::CButtonEx()   
{   
	m_nFlatStyle = CMFCButton::BUTTONSTYLE_NOBORDERS;
	m_bTransparent = false;
	m_FaceColor=NM_BLUE;
	m_TextColor=NM_PAPER;
}
   
CButtonEx::~CButtonEx()   
{   
}

BEGIN_MESSAGE_MAP(CButtonEx, CMFCButton)   
    //{{AFX_MSG_MAP(CButtonEx)
	ON_WM_MOUSEMOVE()
    //}}AFX_MSG_MAP
END_MESSAGE_MAP()

void CButtonEx::OnMouseMove(UINT nFlags, CPoint point)
{
	CButton::OnMouseMove(nFlags, point);
}

BOOL CButtonEx::EnableWindow(BOOL bEnable)
{
	if (bEnable) {
		SetTextColor(m_TextColor);
		SetFaceColor(m_FaceColor, true);
	}
	else {
//		SetTextColor(RGB(123, 123, 123));
		SetTextColor(RGB(0, 0, 0));
		SetFaceColor(RGB(222, 222, 222), true);
	}
	return CMFCButton::EnableWindow(bEnable);
}

void CButtonEx::DrawItem(LPDRAWITEMSTRUCT item) {
    CDC dc;dc.Attach(item->hDC);CRect rect=item->rcItem;dc.FillSolidRect(rect,NM_CANVAS);rect.DeflateRect(1,1);
    bool disabled=(item->itemState&ODS_DISABLED)!=0;bool focused=(item->itemState&ODS_FOCUS)!=0;
    COLORREF face=disabled?NM_TINT:m_FaceColor;
    if(!disabled && (item->itemState&ODS_SELECTED))face=m_FaceColor==NM_BLUE?NM_BLUE_DARK:m_FaceColor;
    NetmindRounded(dc,rect,face,focused?NM_BLUE:face,12,focused?2:1);
    dc.SetBkMode(TRANSPARENT);dc.SetTextColor(disabled?NM_MUTED:m_TextColor);CFont* old=dc.SelectObject(GetFont());
    CString text;GetWindowText(text);dc.DrawText(text,rect,DT_CENTER|DT_VCENTER|DT_SINGLELINE);
    if(focused){CRect focus=rect;focus.DeflateRect(5,5);dc.DrawFocusRect(focus);}
    dc.SelectObject(old);dc.Detach();
}
