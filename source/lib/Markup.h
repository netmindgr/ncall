// Markup.h: small XML document class for nCall.
// Copyright (C) 2026 netmind. GPL-3.0-or-later.
//
// Written from scratch to replace the CMarkup Lite class shipped with MicroSIP,
// whose licence does not allow redistribution under the GPL. It provides the
// subset of the CMarkup programming interface used by the application
// (contacts file, contacts import/export, users directory), with the same
// "main / child / parent position" navigation model.

#pragma once

#include <afxtempl.h>
#include <vector>

class CMarkup
{
public:
    CMarkup();
    CMarkup(const CMarkup& other);
    CMarkup& operator=(const CMarkup& other);
    ~CMarkup();

    // Document
    bool SetDoc(LPCTSTR szDoc);          // parse; false if the text is not well-formed XML
    CString GetDoc() const;              // serialise (no XML declaration)
    bool IsWellFormed() const { return m_root != nullptr; }

    // Navigation at the current level
    bool FindElem(LPCTSTR szName = NULL);       // next sibling of the main position
    bool FindChildElem(LPCTSTR szName = NULL);  // next child of the main element
    bool IntoElem();                            // main becomes parent, child becomes main
    bool OutOfElem();                           // back up one level
    void ResetChildPos() { m_child = -1; }
    void ResetMainPos() { m_main = -1; m_child = -1; }
    void ResetPos() { m_parent = nullptr; m_main = -1; m_child = -1; }

    // Data of the main / child element
    CString GetTagName() const;
    CString GetChildTagName() const;
    CString GetData() const;
    CString GetChildData() const;
    CString GetAttrib(LPCTSTR szAttrib) const;
    CString GetChildAttrib(LPCTSTR szAttrib) const;
    bool FindAttrib(LPCTSTR szAttrib) const;    // main element has this attribute

    // Building
    bool AddElem(LPCTSTR szName, LPCTSTR szData = NULL);       // after the main position
    bool AddChildElem(LPCTSTR szName, LPCTSTR szData = NULL);  // after the child position
    bool AddAttrib(LPCTSTR szAttrib, LPCTSTR szValue) { return SetAttrib(szAttrib, szValue); }
    bool SetAttrib(LPCTSTR szAttrib, LPCTSTR szValue);
    bool AddChildAttrib(LPCTSTR szAttrib, LPCTSTR szValue) { return SetChildAttrib(szAttrib, szValue); }
    bool SetChildAttrib(LPCTSTR szAttrib, LPCTSTR szValue);

private:
    struct Node {
        CString name;
        CString text;
        std::vector<std::pair<CString, CString>> attribs;
        std::vector<Node*> children;
        Node* parent = nullptr;
        ~Node();
        Node* Clone(Node* newParent) const;
    };

    // Level being navigated: the children of m_parent, or the document root when m_parent is null.
    Node* m_root;
    Node* m_parent;
    int m_main;   // index of the main element in the current level, -1 = none
    int m_child;  // index of the child element inside the main element, -1 = none

    void Clear();
    Node* Main() const;
    Node* Child() const;
    int LevelCount() const;
    Node* LevelAt(int index) const;
    static bool NameMatches(const Node* node, LPCTSTR szName);
    static bool SetAttribOn(Node* node, LPCTSTR szAttrib, LPCTSTR szValue);
    static CString AttribOf(const Node* node, LPCTSTR szAttrib);
    static void Write(const Node* node, CString& out);
    static CString Escape(const CString& value, bool attribute);

    // Parser
    class Parser;
};
