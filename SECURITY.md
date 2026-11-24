# Security Policy

*Read this in other languages: [🇬🇧 English](SECURITY.md) | [🇹🇷 Türkçe](SECURITY.tr.md)*

## 🛡️ Reporting a Vulnerability

If you discover a security vulnerability or sensitive data issue in Stokendra, please report it directly to the developer rather than creating a public issue.

- **Developer:** [Tolgahan Acar (GitHub Profile)](https://github.com/tolgahanacar)
- **Security Advisory:** You can submit a confidential report via [GitHub Security Advisories](https://github.com/tolgahanacar/Stokendra/security/advisories/new) or contact the developer directly.
- Reported issues will be assessed promptly, and verified patches will be published in the subsequent release.

---

## 🔒 Supported Versions

Security updates and critical bug fixes are only provided for the latest release:

| Version | Supported |
| :--- | :--- |
| **v7.5.x (.NET 10 LTS)** | :white_check_mark: Yes |
| < v7.5.0 | :x: No (Legacy) |

---

## ⚠️ Legal Terms of Use & Disclaimer of Liability

Please read these conditions carefully before deploying or using Stokendra:

### 1. Free Use & Intellectual Property
Stokendra is provided free of charge for individual and internal operational workflows. All intellectual property, copyright, and exclusive development rights remain with **Tolgahan Acar**.

### 2. Prohibition of Unauthorized Forking & Redistribution
Without explicit, prior written consent from Tolgahan Acar:
- Modifying, patching, or extending the source code,
- Forking the repository for public redistribution or derivative products,
- Redistributing binary setup files via third-party repositories or mirrors,
- Sublicensing, renting, or selling the software

**is strictly prohibited.**

### 3. "AS-IS" Disclaimer & User Responsibility
- **Provided "AS-IS":** This software is provided without any warranty, express or implied.
- **Data Loss & Security Risk:** The author assumes no legal or financial liability for any data loss, database corruption, operational downtime, security incidents, or business interruptions resulting from using this software.
- **Recommendation:** Users are strongly advised to regularly utilize the built-in backup functionality to backup the SQLite database (`stok.db`) to secure external storage.
