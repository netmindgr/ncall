# nCall by netmind

Εφαρμογή τηλεφωνίας SIP (softphone) για Windows 10 και 11, στα ελληνικά. Συνδέεται σε οποιοδήποτε τηλεφωνικό κέντρο ή πάροχο SIP.

**[Λήψη της τελευταίας έκδοσης](https://github.com/netmindgr/ncall/releases/latest/download/ncall-setup.exe)** · [Όλες οι εκδόσεις](https://github.com/netmindgr/ncall/releases) · [Αλλαγές](CHANGELOG.md) · [English](#english)

## Λειτουργίες

- **Κλήσεις:** εισερχόμενες και εξερχόμενες κλήσεις, αναμονή, μεταφορά, προώθηση, συνδιάσκεψη και «Μην ενοχλείτε».
- **Ασφάλεια:** κρυπτογράφηση σήματος (TLS) με έλεγχο πιστοποιητικού και κρυπτογράφηση ήχου (SRTP).
- **Λογαριασμοί:**
  - ρύθμιση με ένα αρχείο `.ncall` από τον πάροχο ή τη netmind: διπλό κλικ και πληκτρολογείτε μόνο τον κωδικό·
  - έτοιμη ρύθμιση για επιλεγμένους παρόχους·
  - πολλοί λογαριασμοί, με εναλλαγή από το μενού.
- **Επαφές:**
  - εισαγωγή από Google Contacts και Outlook (αρχείο CSV)·
  - αναζήτηση, ευρετήριο γραμμάτων και ιστορικό κλήσεων·
  - κλήση με ένα κλικ από συνδέσμους `tel:` και `sip:`.
- **SMS:** αποστολή μέσω του δικού σας λογαριασμού σε πάροχο SMS, με έτοιμα μηνύματα, επιβεβαίωση παράδοσης και ιστορικό. Από το ιστορικό κλήσεων στέλνετε SMS σε κινητό, π.χ. σε αναπάντητη.
- **Βοήθεια:** κλήση στη netmind, απομακρυσμένη βοήθεια και διαγνωστικά χωρίς κωδικούς και αριθμούς, από το μενού.
- **Εμφάνιση:** φωτεινό, σκοτεινό ή αυτόματο θέμα. Το κείμενο εμφανίζεται καθαρό σε οθόνες με διαφορετική κλιμάκωση.
- **Ενημερώσεις:** αυτόματα από αυτή τη σελίδα, ποτέ κατά τη διάρκεια κλήσης, με έλεγχο ακεραιότητας SHA-256.

## Εγκατάσταση

1. Κατεβάστε το [`ncall-setup.exe`](https://github.com/netmindgr/ncall/releases/latest/download/ncall-setup.exe) και τρέξτε το. Δεν χρειάζονται δικαιώματα διαχειριστή.
2. Ανοίξτε το nCall. Αν σας δόθηκε αρχείο `.ncall`, κάντε διπλό κλικ σε αυτό. Αλλιώς, στη φόρμα λογαριασμού διαλέξτε πάροχο ή συμπληρώστε τα στοιχεία SIP που σας δόθηκαν.

Τα αρχεία δεν έχουν ακόμη ψηφιακή υπογραφή. Αν τα Windows εμφανίσουν «Τα Windows προστάτευσαν τον υπολογιστή σας», επιλέξτε **Περισσότερες πληροφορίες → Εκτέλεση ούτως ή άλλως**.

Υπάρχει και έκδοση χωρίς εγκατάσταση (`portable.zip`) στη σελίδα κάθε έκδοσης.

**Έλεγχος γνησιότητας:** κάθε έκδοση έχει αρχείο `SHA256SUMS` με τα αποτυπώματα των αρχείων. Από την 0.3.0 τα αρχεία χτίζονται στο GitHub Actions και συνοδεύονται από πιστοποιητικό προέλευσης, που ελέγχεται με την εντολή `gh attestation verify ncall-setup.exe --repo netmindgr/ncall`.

Απαιτήσεις: Windows 10 έκδοση 2004 ή νεότερη ή Windows 11, και .NET Framework 4.8 (περιλαμβάνεται στα Windows).

## Απόρρητο

Το nCall δεν στέλνει στοιχεία χρήσης. Επικοινωνεί μόνο με:
- τον server SIP που ορίζετε·
- τον πάροχο SMS που ορίζετε, αν χρησιμοποιείτε SMS·
- αυτή τη σελίδα του GitHub, για τον έλεγχο ενημερώσεων. Ο έλεγχος απενεργοποιείται από τις Ρυθμίσεις (Ενημερώσεις: «Ποτέ»).

Κωδικοί και κλειδιά αποθηκεύονται κρυπτογραφημένα με τα μέσα των Windows (DPAPI).

## Πολιτική υπογραφής κώδικα (Code signing policy)

Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).

- **Τι υπογράφεται:** μόνο τα αρχεία που χτίζονται από αυτό το repository (`ncall.exe`, `netmind.ui.dll` και ο installer `ncall-setup.exe`). Τα αρχεία κατασκευάζονται αυτόματα στο [GitHub Actions](.github/workflows/release.yml) από τον δημόσιο κώδικα κάθε έκδοσης. Αρχεία που χτίζονται τοπικά δεν υπογράφονται ποτέ.
- **Ρόλοι:**
  - Committers και reviewers: [netmind](https://github.com/netmindgr)
  - Approvers: Ηλίας Παπαντωνίου ([netmind](https://www.netmind.gr/))
- **Έλεγχος αλλαγών:** κάθε αλλαγή από τρίτους (pull request) ελέγχεται από μέλος της ομάδας πριν γίνει δεκτή. Κάθε υπογραφή έκδοσης εγκρίνεται χειροκίνητα.
- **Απόρρητο:** This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it. Οι μόνες συνδέσεις είναι αυτές της ενότητας [Απόρρητο](#απόρρητο).

Τα αρχεία υπογράφονται από την πρώτη έκδοση που θα κυκλοφορήσει μετά την έγκριση του έργου από το SignPath Foundation. Στον υπογεγραμμένο installer, ως εκδότης εμφανίζεται το «SignPath Foundation».

## Πηγαίος κώδικας και άδεια

Το nCall βασίζεται στα [MicroSIP](https://www.microsip.org/) και [PJSIP](https://www.pjsip.org/) και διανέμεται με άδεια **GPL-3.0-or-later**. Οι άδειες των εξαρτήσεων υπάρχουν στα `NOTICE.txt`, `dependency-versions.txt` και `third_party/netmind/licenses`. Κάθε έκδοση συνοδεύεται από τον πλήρη πηγαίο κώδικά της (`source.zip`).

Οδηγίες κατασκευής: [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## Υποστήριξη

Το nCall αναπτύσσεται και συντηρείται από τη **[netmind](https://www.netmind.gr/)**, Βέροια. Η netmind αναλαμβάνει:
- το στήσιμό του σε επιχειρήσεις και την υποστήριξη·
- τη διάθεση γραμμών τηλεφωνίας και πακέτων SMS.

[netmind.gr](https://www.netmind.gr/) · info@netmind.gr · 2331 200260

## English

nCall is a free and open-source SIP softphone for Windows 10 and 11 with a Greek user interface. It works with any SIP provider or PBX. It is based on [MicroSIP](https://www.microsip.org/) and [PJSIP](https://www.pjsip.org/) and is licensed under **GPL-3.0-or-later**.

- **Features:** incoming and outgoing calls, hold, transfer, forwarding, conference, do-not-disturb; TLS with certificate verification and SRTP; multiple accounts and one-click account setup files (`.ncall`, never containing the password); contacts import from Google Contacts and Outlook (CSV); `tel:` and `sip:` links; optional SMS sending through the user's own SMS provider account, with ready-made messages; light, dark and automatic theme.
- **Download:** [`ncall-setup.exe`](https://github.com/netmindgr/ncall/releases/latest/download/ncall-setup.exe) (per-user installer, no administrator rights) or the portable zip from the [releases page](https://github.com/netmindgr/ncall/releases). Each release includes its complete source code (`source.zip`) and SHA-256 checksums.
- **Build:** release files are built by [GitHub Actions](.github/workflows/release.yml) on GitHub-hosted runners from this repository. Starting with 0.3.0 every release carries a build provenance attestation: `gh attestation verify <file> --repo netmindgr/ncall`. Build instructions: [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).
- **Privacy:** nCall sends no usage data. It connects only to the SIP server and the SMS provider configured by the user, and to this GitHub repository to check for updates (can be turned off in Settings). Passwords and keys are stored encrypted with Windows DPAPI.

### Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).

- **Signed files:** only files built from this repository: `ncall.exe`, `netmind.ui.dll` and the installer `ncall-setup.exe`. They are built by the release workflow on GitHub-hosted runners; locally built files are never signed.
- **Team roles:**
  - Committers and reviewers: [netmind](https://github.com/netmindgr)
  - Approvers: Ilias Papantoniou ([netmind](https://www.netmind.gr/))
- **Review:** every external contribution (pull request) is reviewed by a team member before it is accepted. Every signing request is approved manually.
- **Privacy:** This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it.

Signing starts with the first release after the project is accepted by SignPath Foundation.

### Support and security

nCall is developed and maintained by [netmind](https://www.netmind.gr/), Veria, Greece. Bug reports: [issues](https://github.com/netmindgr/ncall/issues). Security issues: see [SECURITY.md](SECURITY.md). Contact: info@netmind.gr.
