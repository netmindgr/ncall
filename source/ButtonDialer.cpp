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

#include "stdafx.h"
#include "ButtonDialer.h"
#include "NetmindTheme.h"
#include "Strsafe.h"
#include "const.h"

/////////////////////////////////////////////////////////////////////////////
// CButtonDialer

CButtonDialer::CButtonDialer()
{
	forceNumeric = false;
	m_map.SetAt(_T("1"), _T(""));
	m_map.SetAt(_T("2"), _T("ABC"));
	m_map.SetAt(_T("3"), _T("DEF"));
	m_map.SetAt(_T("4"), _T("GHI"));
	m_map.SetAt(_T("5"), _T("JKL"));
	m_map.SetAt(_T("6"), _T("MNO"));
	m_map.SetAt(_T("7"), _T("PQRS"));
	m_map.SetAt(_T("8"), _T("TUV"));
	m_map.SetAt(_T("9"), _T("WXYZ"));
	m_map.SetAt(_T("0"), _T(""));
	m_map.SetAt(_T("*"), _T(""));
	m_map.SetAt(_T("#"), _T(""));
}

CButtonDialer::~CButtonDialer()
{
	CloseTheme();
}


BEGIN_MESSAGE_MAP(CButtonDialer, CButton)
	ON_WM_THEMECHANGED()
	ON_WM_MOUSEMOVE()
	ON_WM_SIZE()
END_MESSAGE_MAP()

/////////////////////////////////////////////////////////////////////////////
// CButtonDialer message handlers

void CButtonDialer::PreSubclassWindow()
{
	OpenTheme();

	HFONT hFont = (HFONT)GetStockObject(DEFAULT_GUI_FONT);
	LOGFONT lf;
	GetObject(hFont, sizeof(LOGFONT), &lf);
	lf.lfHeight = 12;
	CDC *pDC = GetDC();
	if (pDC) {
		dpiY = GetDeviceCaps(pDC->m_hDC, LOGPIXELSY);
		lf.lfHeight = MulDiv(lf.lfHeight, dpiY, 96);
		ReleaseDC(pDC);
	}
	else {
		dpiY = 96;
	}
	StringCchCopy(lf.lfFaceName, LF_FACESIZE, _T("Open Sans"));
	m_FontLetters.CreateFontIndirect(&lf);

	DWORD dwStyle = ::GetClassLong(m_hWnd, GCL_STYLE);
	dwStyle &= ~CS_DBLCLKS;
	::SetClassLong(m_hWnd, GCL_STYLE, dwStyle);
}

LRESULT CButtonDialer::OnThemeChanged()
{
	CloseTheme();
	OpenTheme();
	return 0L;
}

void CButtonDialer::OnSize(UINT type, int w, int h)
{
	CButton::OnSize(type, w, h);
}

void CButtonDialer::OnMouseMove(UINT nFlags, CPoint point)
{
	CRect rect;
	GetClientRect(&rect);
	if (rect.PtInRect(point)) {
		if (GetCapture() != this) {
			SetCapture();
			Invalidate();
		}
	}
	else {
		ReleaseCapture();
		Invalidate();
	}
	CButton::OnMouseMove(nFlags, point);
}

// netmind keyboard rendering, 2026-10-12.
void CButtonDialer::DrawItem(LPDRAWITEMSTRUCT item) {
    CDC dc;dc.Attach(item->hDC);CRect rect=item->rcItem;
    dc.FillSolidRect(rect,NM_CANVAS);rect.DeflateRect(1,1);
    bool pressed=(item->itemState&ODS_SELECTED)!=0;
    bool focused=(item->itemState&ODS_FOCUS)!=0;
    bool hot=GetCapture()==this;
    NetmindRounded(dc,rect,pressed?RGB(219,234,254):hot?NM_TINT:NM_PAPER,focused?NM_BLUE:NM_LINE,12,focused?2:1);
    dc.SetBkMode(TRANSPARENT);dc.SetTextColor(NM_INK);
    CString text,letters;GetWindowText(text);
    CFont* old=dc.SelectObject(GetFont());
    CRect content=rect;content.DeflateRect(4,3);
    if(!forceNumeric && m_map.Lookup(text,letters)) {
        if(!letters.IsEmpty())content.bottom-=MulDiv(12,dpiY,96);
        dc.DrawText(text,content,DT_CENTER|DT_VCENTER|DT_SINGLELINE);
        if(!letters.IsEmpty()) {
            dc.SelectObject(&m_FontLetters);dc.SetTextColor(NM_MUTED);
            content=rect;content.top=rect.bottom-MulDiv(18,dpiY,96);content.bottom-=3;
            dc.DrawText(letters,content,DT_CENTER|DT_VCENTER|DT_SINGLELINE);
        }
    } else {
        if(text.GetLength()>2)dc.SelectObject(&m_FontLetters);
        dc.SetTextColor(NM_MUTED);dc.DrawText(text,content,DT_CENTER|DT_VCENTER|DT_SINGLELINE);
    }
    if(focused) {CRect focus=rect;focus.DeflateRect(5,5);dc.DrawFocusRect(focus);}
    dc.SelectObject(old);dc.Detach();
}
