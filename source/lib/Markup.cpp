// Markup.cpp: small XML document class for nCall.
// Copyright (C) 2026 netmind. GPL-3.0-or-later. See Markup.h.

#include "stdafx.h"
#include "Markup.h"

// ---------------------------------------------------------------- nodes

CMarkup::Node::~Node()
{
    for (Node* child : children) {
        delete child;
    }
}

CMarkup::Node* CMarkup::Node::Clone(Node* newParent) const
{
    Node* copy = new Node();
    copy->name = name;
    copy->text = text;
    copy->attribs = attribs;
    copy->parent = newParent;
    for (const Node* child : children) {
        copy->children.push_back(child->Clone(copy));
    }
    return copy;
}

// ---------------------------------------------------------------- parser

class CMarkup::Parser
{
public:
    explicit Parser(LPCTSTR text) : p(text) {}

    // Returns the root element, or null if the document is not well-formed.
    Node* Document()
    {
        Node* root = nullptr;
        for (;;) {
            SkipSpace();
            if (!*p) {
                break;
            }
            if (*p != _T('<')) {
                delete root;
                return nullptr;  // text outside the root element
            }
            if (Starts(_T("<?"))) {
                if (!SkipPast(_T("?>"))) break;
            }
            else if (Starts(_T("<!--"))) {
                if (!SkipPast(_T("-->"))) break;
            }
            else if (Starts(_T("<!"))) {
                if (!SkipDeclaration()) break;
            }
            else {
                if (root) {  // a second root element
                    delete root;
                    return nullptr;
                }
                root = Element(nullptr);
                if (!root) {
                    return nullptr;
                }
            }
        }
        if (failed) {
            delete root;
            return nullptr;
        }
        return root;
    }

private:
    LPCTSTR p;
    bool failed = false;

    bool Starts(LPCTSTR s) const { return _tcsncmp(p, s, _tcslen(s)) == 0; }
    void SkipSpace() { while (*p && _istspace(*p)) p++; }
    bool SkipPast(LPCTSTR s)
    {
        LPCTSTR end = _tcsstr(p, s);
        if (!end) { failed = true; return false; }
        p = end + _tcslen(s);
        return true;
    }
    bool SkipDeclaration()
    {
        // <!DOCTYPE ...> possibly with an internal subset [ ... ]
        int depth = 0;
        while (*p) {
            if (*p == _T('[')) depth++;
            else if (*p == _T(']')) depth--;
            else if (*p == _T('>') && depth <= 0) { p++; return true; }
            p++;
        }
        failed = true;
        return false;
    }
    static bool NameChar(TCHAR c)
    {
        return _istalnum(c) || c == _T('_') || c == _T('-') || c == _T('.') || c == _T(':') || c > 0x7f;
    }
    CString Name()
    {
        LPCTSTR start = p;
        while (*p && NameChar(*p)) p++;
        return CString(start, (int)(p - start));
    }
    static void AppendCodePoint(CString& out, unsigned long cp)
    {
        if (cp >= 0x10000 && cp <= 0x10FFFF) {
            cp -= 0x10000;
            out.AppendChar((TCHAR)(0xD800 + (cp >> 10)));
            out.AppendChar((TCHAR)(0xDC00 + (cp & 0x3FF)));
        }
        else if (cp > 0 && cp < 0x10000) {
            out.AppendChar((TCHAR)cp);
        }
    }
    // Text up to 'stop' (exclusive) with entity references decoded.
    bool Decoded(TCHAR stop, CString& out)
    {
        while (*p && *p != stop) {
            if (*p == _T('<') && stop != _T('<')) { failed = true; return false; }
            if (*p == _T('&')) {
                LPCTSTR semi = _tcschr(p, _T(';'));
                if (!semi || semi - p > 12) { out.AppendChar(*p++); continue; }
                CString ent(p + 1, (int)(semi - p - 1));
                if (ent == _T("lt")) out.AppendChar(_T('<'));
                else if (ent == _T("gt")) out.AppendChar(_T('>'));
                else if (ent == _T("amp")) out.AppendChar(_T('&'));
                else if (ent == _T("quot")) out.AppendChar(_T('"'));
                else if (ent == _T("apos")) out.AppendChar(_T('\''));
                else if (ent.GetLength() > 1 && ent[0] == _T('#')) {
                    bool hex = ent[1] == _T('x') || ent[1] == _T('X');
                    AppendCodePoint(out, _tcstoul((LPCTSTR)ent + (hex ? 2 : 1), NULL, hex ? 16 : 10));
                }
                else { out.Append(CString(p, (int)(semi - p + 1))); }  // unknown entity: keep as is
                p = semi + 1;
                continue;
            }
            out.AppendChar(*p++);
        }
        return true;
    }
    Node* Fail(Node* node) { delete node; failed = true; return nullptr; }

    Node* Element(Node* parent)
    {
        // at '<'
        p++;
        Node* node = new Node();
        node->parent = parent;
        node->name = Name();
        if (node->name.IsEmpty()) return Fail(node);
        for (;;) {
            SkipSpace();
            if (Starts(_T("/>"))) { p += 2; return node; }
            if (*p == _T('>')) { p++; break; }
            CString attr = Name();
            if (attr.IsEmpty()) return Fail(node);
            SkipSpace();
            if (*p != _T('=')) return Fail(node);
            p++;
            SkipSpace();
            TCHAR quote = *p;
            if (quote != _T('"') && quote != _T('\'')) return Fail(node);
            p++;
            CString value;
            if (!Decoded(quote, value) || *p != quote) return Fail(node);
            p++;
            node->attribs.push_back(std::make_pair(attr, value));
        }
        // content
        for (;;) {
            if (!*p) return Fail(node);
            if (Starts(_T("</"))) {
                p += 2;
                CString closing = Name();
                SkipSpace();
                if (closing != node->name || *p != _T('>')) return Fail(node);
                p++;
                return node;
            }
            if (Starts(_T("<!--"))) {
                if (!SkipPast(_T("-->"))) return Fail(node);
                continue;
            }
            if (Starts(_T("<![CDATA["))) {
                p += 9;
                LPCTSTR end = _tcsstr(p, _T("]]>"));
                if (!end) return Fail(node);
                node->text.Append(CString(p, (int)(end - p)));
                p = end + 3;
                continue;
            }
            if (Starts(_T("<?"))) {
                if (!SkipPast(_T("?>"))) return Fail(node);
                continue;
            }
            if (*p == _T('<')) {
                Node* child = Element(node);
                if (!child) return Fail(node);
                node->children.push_back(child);
                continue;
            }
            if (!Decoded(_T('<'), node->text)) return Fail(node);
        }
    }
};

// ---------------------------------------------------------------- document

CMarkup::CMarkup() : m_root(nullptr), m_parent(nullptr), m_main(-1), m_child(-1) {}

CMarkup::CMarkup(const CMarkup& other) : m_root(nullptr), m_parent(nullptr), m_main(-1), m_child(-1)
{
    *this = other;
}

CMarkup& CMarkup::operator=(const CMarkup& other)
{
    if (this != &other) {
        Clear();
        m_root = other.m_root ? other.m_root->Clone(nullptr) : nullptr;
        // positions are not carried over
    }
    return *this;
}

CMarkup::~CMarkup()
{
    Clear();
}

void CMarkup::Clear()
{
    delete m_root;
    m_root = nullptr;
    ResetPos();
}

bool CMarkup::SetDoc(LPCTSTR szDoc)
{
    Clear();
    if (!szDoc || !*szDoc) {
        return true;  // empty document
    }
    LPCTSTR text = szDoc;
    if (*text == 0xFEFF) text++;  // byte order mark
    Parser parser(text);
    m_root = parser.Document();
    return m_root != nullptr;
}

CString CMarkup::Escape(const CString& value, bool attribute)
{
    CString out;
    for (int i = 0; i < value.GetLength(); i++) {
        TCHAR c = value[i];
        switch (c) {
        case _T('<'): out.Append(_T("&lt;")); break;
        case _T('>'): out.Append(_T("&gt;")); break;
        case _T('&'): out.Append(_T("&amp;")); break;
        case _T('"'): if (attribute) { out.Append(_T("&quot;")); break; } out.AppendChar(c); break;
        case _T('\''): if (attribute) { out.Append(_T("&apos;")); break; } out.AppendChar(c); break;
        case _T('\r'): if (attribute) { out.Append(_T("&#13;")); break; } out.AppendChar(c); break;
        case _T('\n'): if (attribute) { out.Append(_T("&#10;")); break; } out.AppendChar(c); break;
        default: out.AppendChar(c);
        }
    }
    return out;
}

void CMarkup::Write(const Node* node, CString& out)
{
    out.AppendChar(_T('<'));
    out.Append(node->name);
    for (const auto& attr : node->attribs) {
        out.AppendFormat(_T(" %s=\"%s\""), (LPCTSTR)attr.first, (LPCTSTR)Escape(attr.second, true));
    }
    if (node->children.empty() && node->text.IsEmpty()) {
        out.Append(_T("/>\r\n"));
        return;
    }
    out.AppendChar(_T('>'));
    if (node->children.empty()) {
        out.Append(Escape(node->text, false));
    }
    else {
        out.Append(_T("\r\n"));
        for (const Node* child : node->children) {
            Write(child, out);
        }
    }
    out.AppendFormat(_T("</%s>\r\n"), (LPCTSTR)node->name);
}

CString CMarkup::GetDoc() const
{
    CString out;
    if (m_root) {
        Write(m_root, out);
    }
    return out;
}

// ---------------------------------------------------------------- navigation

int CMarkup::LevelCount() const
{
    if (m_parent) return (int)m_parent->children.size();
    return m_root ? 1 : 0;
}

CMarkup::Node* CMarkup::LevelAt(int index) const
{
    if (index < 0 || index >= LevelCount()) return nullptr;
    return m_parent ? m_parent->children[index] : m_root;
}

CMarkup::Node* CMarkup::Main() const
{
    return LevelAt(m_main);
}

CMarkup::Node* CMarkup::Child() const
{
    Node* main = Main();
    if (!main || m_child < 0 || m_child >= (int)main->children.size()) return nullptr;
    return main->children[m_child];
}

bool CMarkup::NameMatches(const Node* node, LPCTSTR szName)
{
    return !szName || !*szName || node->name == szName;
}

bool CMarkup::FindElem(LPCTSTR szName)
{
    for (int i = m_main + 1; i < LevelCount(); i++) {
        if (NameMatches(LevelAt(i), szName)) {
            m_main = i;
            m_child = -1;
            return true;
        }
    }
    return false;  // position unchanged
}

bool CMarkup::FindChildElem(LPCTSTR szName)
{
    if (!Main() && !FindElem()) {  // no main position yet: use the first element
        return false;
    }
    Node* main = Main();
    for (int i = m_child + 1; i < (int)main->children.size(); i++) {
        if (NameMatches(main->children[i], szName)) {
            m_child = i;
            return true;
        }
    }
    return false;  // position unchanged
}

bool CMarkup::IntoElem()
{
    Node* main = Main();
    if (!main) return false;
    m_parent = main;
    m_main = m_child;
    m_child = -1;
    return true;
}

bool CMarkup::OutOfElem()
{
    if (!m_parent) return false;
    Node* old = m_parent;
    m_parent = old->parent;
    m_child = m_main;
    m_main = -1;
    for (int i = 0; i < LevelCount(); i++) {
        if (LevelAt(i) == old) {
            m_main = i;
            break;
        }
    }
    return true;
}

// ---------------------------------------------------------------- data

CString CMarkup::AttribOf(const Node* node, LPCTSTR szAttrib)
{
    if (node && szAttrib) {
        for (const auto& attr : node->attribs) {
            if (attr.first == szAttrib) return attr.second;
        }
    }
    return CString();
}

CString CMarkup::GetTagName() const { Node* n = Main(); return n ? n->name : CString(); }
CString CMarkup::GetChildTagName() const { Node* n = Child(); return n ? n->name : CString(); }
CString CMarkup::GetData() const { Node* n = Main(); return n ? n->text : CString(); }
CString CMarkup::GetChildData() const { Node* n = Child(); return n ? n->text : CString(); }
CString CMarkup::GetAttrib(LPCTSTR szAttrib) const { return AttribOf(Main(), szAttrib); }
CString CMarkup::GetChildAttrib(LPCTSTR szAttrib) const { return AttribOf(Child(), szAttrib); }

bool CMarkup::FindAttrib(LPCTSTR szAttrib) const
{
    Node* main = Main();
    if (!main || !szAttrib) return false;
    for (const auto& attr : main->attribs) {
        if (attr.first == szAttrib) return true;
    }
    return false;
}

// ---------------------------------------------------------------- building

bool CMarkup::SetAttribOn(Node* node, LPCTSTR szAttrib, LPCTSTR szValue)
{
    if (!node || !szAttrib || !*szAttrib) return false;
    CString value = szValue ? szValue : _T("");
    for (auto& attr : node->attribs) {
        if (attr.first == szAttrib) {
            attr.second = value;
            return true;
        }
    }
    node->attribs.push_back(std::make_pair(CString(szAttrib), value));
    return true;
}

bool CMarkup::SetAttrib(LPCTSTR szAttrib, LPCTSTR szValue) { return SetAttribOn(Main(), szAttrib, szValue); }
bool CMarkup::SetChildAttrib(LPCTSTR szAttrib, LPCTSTR szValue) { return SetAttribOn(Child(), szAttrib, szValue); }

bool CMarkup::AddElem(LPCTSTR szName, LPCTSTR szData)
{
    if (!szName || !*szName) return false;
    Node* node = new Node();
    node->name = szName;
    if (szData) node->text = szData;
    if (!m_parent) {
        if (m_root) {  // only one root element
            delete node;
            return false;
        }
        m_root = node;
        m_main = 0;
    }
    else {
        node->parent = m_parent;
        int at = m_main < 0 ? (int)m_parent->children.size() : m_main + 1;
        m_parent->children.insert(m_parent->children.begin() + at, node);
        m_main = at;
    }
    m_child = -1;
    return true;
}

bool CMarkup::AddChildElem(LPCTSTR szName, LPCTSTR szData)
{
    Node* main = Main();
    if (!main || !szName || !*szName) return false;
    Node* node = new Node();
    node->name = szName;
    if (szData) node->text = szData;
    node->parent = main;
    int at = m_child < 0 ? (int)main->children.size() : m_child + 1;
    main->children.insert(main->children.begin() + at, node);
    m_child = at;
    return true;
}
