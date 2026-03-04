using System.Text;
using Microsoft.Data.Sqlite;
using static StokTakip.LocalizationManager;

namespace StokTakip.Forms;

public class AyarlarPanel : UserControl
{
    private ComboBox cmbDil = new();
    private TextBox txtFirma = new(), txtDbPath = new();

    public AyarlarPanel()
    {
        BackColor = UIHelper.BgDark; Dock = DockStyle.Fill; DoubleBuffered = true;

        var pnlH = UIHelper.MakeHeader(L("settings_title"));

        var pnlBody = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.BgDark, Padding = new Padding(28, 12, 28, 12), AutoScroll = true };
        int y = 10, lw = 200, fw = 350;

        // ═══ DİL ═══
        pnlBody.Controls.Add(SectionHeader("🌐", L("language_label"), UIHelper.AccentCyan, ref y));
        pnlBody.Controls.Add(MakeLbl(L("language_label"), 0, y));
        cmbDil = new ComboBox { Left = lw, Top = y, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
        UIHelper.StyleComboBox(cmbDil);
        for (int i = 0; i < LocalizationManager.SupportedLanguages.Length; i++) cmbDil.Items.Add(LocalizationManager.LanguageDisplayNames[i]);
        int li = Array.IndexOf(LocalizationManager.SupportedLanguages, Program.Settings.Language);
        cmbDil.SelectedIndex = li >= 0 ? li : 0;
        pnlBody.Controls.Add(cmbDil); y += 44;

        // ═══ FİRMA ═══
        pnlBody.Controls.Add(MakeDiv(ref y));
        pnlBody.Controls.Add(SectionHeader("🏢", L("company_name_label"), UIHelper.AccentBlue, ref y));
        pnlBody.Controls.Add(MakeLbl(L("company_name_label"), 0, y));
        txtFirma = new TextBox { Left = lw, Top = y, Width = fw }; txtFirma.Text = Program.Settings.CompanyName; UIHelper.StyleTextBox(txtFirma);
        pnlBody.Controls.Add(txtFirma); y += 26;
        pnlBody.Controls.Add(new Label { Text = L("company_name_hint"), Left = lw, Top = y, Font = new Font("Segoe UI", 7.5f), ForeColor = UIHelper.TextDim, AutoSize = true }); y += 28;

        // ═══ VERİTABANI YOLU ═══
        pnlBody.Controls.Add(MakeDiv(ref y));
        pnlBody.Controls.Add(SectionHeader("🗄", L("db_path_label"), UIHelper.AccentPurple, ref y));
        pnlBody.Controls.Add(MakeLbl(L("db_path_label"), 0, y));
        txtDbPath = new TextBox { Left = lw, Top = y, Width = fw - 100, ReadOnly = true }; txtDbPath.Text = Program.Settings.DbPath;
        UIHelper.StyleTextBox(txtDbPath); txtDbPath.ForeColor = UIHelper.TextDim;
        var btnDbDeg = UIHelper.MakeButton(L("change_db"), UIHelper.BtnMid, lw + fw - 90, y - 2, 90, 28);
        btnDbDeg.Click += (_, _) => { using var d = new SaveFileDialog { Title = L("db_location"), Filter = "SQLite DB|*.db", FileName = "stok.db" }; if (d.ShowDialog() == DialogResult.OK) txtDbPath.Text = d.FileName; };
        pnlBody.Controls.AddRange(new Control[] { txtDbPath, btnDbDeg }); y += 44;

        // ═══ YEDEKLEME ═══
        pnlBody.Controls.Add(MakeDiv(ref y));
        pnlBody.Controls.Add(SectionHeader("💾", L("backup_section"), UIHelper.AccentYellow, ref y));

        var btnBackupDb = UIHelper.MakeButton(L("backup_db"), UIHelper.AccentYellow, 0, y, 240, 36);
        btnBackupDb.ForeColor = Color.Black;
        pnlBody.Controls.Add(btnBackupDb);
        pnlBody.Controls.Add(new Label { Text = L("backup_db_desc"), Left = 250, Top = y + 10, Font = new Font("Segoe UI", 8), ForeColor = UIHelper.TextDim, AutoSize = true });
        btnBackupDb.Click += (_, _) => BackupDb();
        y += 48;

        var btnBackupSql = UIHelper.MakeButton(L("backup_sql"), UIHelper.AccentCyan, 0, y, 240, 36);
        pnlBody.Controls.Add(btnBackupSql);
        pnlBody.Controls.Add(new Label { Text = L("backup_sql_desc"), Left = 250, Top = y + 10, Font = new Font("Segoe UI", 8), ForeColor = UIHelper.TextDim, AutoSize = true });
        btnBackupSql.Click += (_, _) => BackupSql();
        y += 56;

        // ═══ KAYDET ═══
        pnlBody.Controls.Add(MakeDiv(ref y));
        y += 8;
        var btnKaydet = UIHelper.MakeButton(L("save_settings"), UIHelper.AccentGreen, 0, y, 180, 40);
        btnKaydet.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        btnKaydet.Click += KaydetAyarlar;
        pnlBody.Controls.Add(btnKaydet);
        y += 60;

        // ═══ ŞİFRE DEĞİŞTİR ═══
        pnlBody.Controls.Add(MakeDiv(ref y));
        pnlBody.Controls.Add(SectionHeader("\ud83d\udd12", L("change_password"), UIHelper.AccentOrange, ref y));

        pnlBody.Controls.Add(MakeLbl(L("old_password"), 0, y));
        var txtEski = new TextBox { Left = lw, Top = y, Width = fw, UseSystemPasswordChar = true }; UIHelper.StyleTextBox(txtEski);
        pnlBody.Controls.Add(txtEski); y += 34;

        pnlBody.Controls.Add(MakeLbl(L("new_password"), 0, y));
        var txtYeni = new TextBox { Left = lw, Top = y, Width = fw, UseSystemPasswordChar = true }; UIHelper.StyleTextBox(txtYeni);
        pnlBody.Controls.Add(txtYeni); y += 34;

        pnlBody.Controls.Add(MakeLbl(L("confirm_password"), 0, y));
        var txtTekrar = new TextBox { Left = lw, Top = y, Width = fw, UseSystemPasswordChar = true }; UIHelper.StyleTextBox(txtTekrar);
        pnlBody.Controls.Add(txtTekrar); y += 38;

        var btnSifre = UIHelper.MakeButton(L("change_password"), UIHelper.AccentOrange, 0, y, 200, 36);
        btnSifre.ForeColor = Color.Black;
        btnSifre.Click += (_, _) =>
        {
            if (string.IsNullOrEmpty(txtEski.Text) || string.IsNullOrEmpty(txtYeni.Text) || string.IsNullOrEmpty(txtTekrar.Text))
            { MessageBox.Show(L("password_empty")); return; }
            if (txtYeni.Text != txtTekrar.Text)
            { MessageBox.Show(L("password_mismatch")); return; }
            if (Program.DB!.SifreDegistir(Program.CurrentUser, txtEski.Text, txtYeni.Text))
            { MessageBox.Show(L("password_changed"), L("info"), MessageBoxButtons.OK, MessageBoxIcon.Information); txtEski.Clear(); txtYeni.Clear(); txtTekrar.Clear(); }
            else MessageBox.Show(L("password_wrong"), L("error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };
        pnlBody.Controls.Add(btnSifre); y += 56;

        // ═══ HAKKINDA ═══
        pnlBody.Controls.Add(MakeDiv(ref y));
        pnlBody.Controls.Add(SectionHeader("ℹ️", "HAKKINDA", UIHelper.AccentCyan, ref y));

        var pnlAbout = new Panel { Left = 0, Top = y, Width = 550, Height = 130, BackColor = UIHelper.BgCard };
        var accentLine = new Panel { Dock = DockStyle.Left, Width = 4, BackColor = UIHelper.AccentCyan };
        pnlAbout.Controls.Add(accentLine);
        pnlAbout.Controls.Add(new Label { Text = "Stokendra", Left = 20, Top = 12, AutoSize = true, Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = UIHelper.AccentCyan });
        pnlAbout.Controls.Add(new Label { Text = "v1.0 — Envanter Yönetim Sistemi", Left = 130, Top = 20, AutoSize = true, Font = new Font("Segoe UI Semibold", 9), ForeColor = UIHelper.TextSecondary });
        pnlAbout.Controls.Add(new Label { Text = "Geliştirici: Tolgahan Acar", Left = 20, Top = 48, AutoSize = true, Font = new Font("Segoe UI Semibold", 9.5f), ForeColor = UIHelper.TextPrimary });
        pnlAbout.Controls.Add(new Label { Text = "© 2026 Tolgahan Acar. Tüm hakları saklıdır.", Left = 20, Top = 72, AutoSize = true, Font = new Font("Segoe UI", 9), ForeColor = UIHelper.TextSecondary });
        pnlAbout.Controls.Add(new Label { Text = "Bu yazılım lisanslıdır. İzinsiz kopyalanması, dağıtılması veya\ntersine mühendislik yapılması yasaktır.", Left = 20, Top = 96, AutoSize = true, Font = new Font("Segoe UI", 8), ForeColor = UIHelper.TextDim });
        pnlBody.Controls.Add(pnlAbout);

        Controls.Add(pnlBody); Controls.Add(pnlH);
    }

    static Label MakeLbl(string t, int x, int y) => new Label { Text = t, Left = x, Top = y + 3, Width = 190, Font = UIHelper.FontLabel, ForeColor = UIHelper.TextSecondary };
    static Panel MakeDiv(ref int y) { var p = new Panel { Left = 0, Top = y, Width = 600, Height = 1, BackColor = UIHelper.Divider }; y += 14; return p; }
    static Label SectionHeader(string icon, string text, Color c, ref int y) { var l = new Label { Text = $"{icon}  {text.ToUpperInvariant()}", Left = 0, Top = y, AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = c }; y += 30; return l; }

    private void BackupDb()
    {
        using var dlg = new SaveFileDialog { Title = L("backup_db"), Filter = "SQLite DB|*.db", FileName = $"stokendra_yedek_{DateTime.Now:yyyyMMdd_HHmm}.db" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try
        {
            File.Copy(Program.Settings.DbPath, dlg.FileName, true);
            MessageBox.Show(L("backup_success", dlg.FileName), L("info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(L("backup_error", ex.Message), L("error"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void BackupSql()
    {
        using var dlg = new SaveFileDialog { Title = L("backup_sql"), Filter = "SQL|*.sql", FileName = $"stokendra_yedek_{DateTime.Now:yyyyMMdd_HHmm}.sql" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("-- Stokendra SQL Backup");
            sb.AppendLine($"-- Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"-- Developer: Tolgahan Acar");
            sb.AppendLine();

            using var con = new SqliteConnection($"Data Source={Program.Settings.DbPath}");
            con.Open();

            string[] tables = { "StokKartlari", "StokHareketleri", "Notlar", "Birimler", "Departmanlar", "AppConfig", "AuditLog", "Kullanicilar" };
            foreach (var table in tables)
            {
                try
                {
                    sb.AppendLine($"-- Table: {table}");
                    using var cmd = con.CreateCommand();
                    cmd.CommandText = $"SELECT sql FROM sqlite_master WHERE type='table' AND name='{table}'";
                    var createSql = cmd.ExecuteScalar()?.ToString();
                    if (!string.IsNullOrEmpty(createSql))
                    {
                        sb.AppendLine($"DROP TABLE IF EXISTS {table};");
                        sb.AppendLine(createSql + ";");
                        sb.AppendLine();
                    }

                    using var cmd2 = con.CreateCommand();
                    cmd2.CommandText = $"SELECT * FROM {table}";
                    using var r = cmd2.ExecuteReader();
                    while (r.Read())
                    {
                        var vals = new List<string>();
                        for (int i = 0; i < r.FieldCount; i++)
                        {
                            if (r.IsDBNull(i)) vals.Add("NULL");
                            else if (r.GetFieldType(i) == typeof(long) || r.GetFieldType(i) == typeof(double)) vals.Add(r.GetValue(i).ToString()!);
                            else vals.Add($"'{r.GetString(i).Replace("'", "''")}'");
                        }
                        sb.AppendLine($"INSERT INTO {table} VALUES ({string.Join(",", vals)});");
                    }
                    sb.AppendLine();
                }
                catch { /* table may not exist */ }
            }

            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show(L("backup_success", dlg.FileName), L("info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(L("backup_error", ex.Message), L("error"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void KaydetAyarlar(object? s, EventArgs e)
    {
        string secilenDil = LocalizationManager.SupportedLanguages[cmbDil.SelectedIndex];
        bool dilDegisti = secilenDil != Program.Settings.Language;
        Program.Settings.Language = secilenDil;
        Program.Settings.CompanyName = txtFirma.Text.Trim();
        if (!string.IsNullOrWhiteSpace(txtDbPath.Text)) Program.Settings.DbPath = txtDbPath.Text;
        Program.Settings.Kaydet();
        if (dilDegisti) MessageBox.Show(L("saved_restart"), L("info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        else MessageBox.Show(L("settings_saved"), L("info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
