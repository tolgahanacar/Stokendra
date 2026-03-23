using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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

        var pnlH = UIHelper.MakeHeader(L("settings_title"), L("settings_subtitle"));

        var pnlScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = UIHelper.BgDark, Padding = new Padding(28, 12, 28, 20) };
        var tbl = new TableLayoutPanel 
        { 
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 3,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent
        };
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

        // ═══ GENEL AYARLAR ═══
        FlowLayoutPanel bodyGenel;
        var cardGenel = UIHelper.MakeSettingsGroup(L("general_status"), "⚙", UIHelper.AccentCyan, out bodyGenel, 400);
        cardGenel.Dock = DockStyle.Fill;
        
        bodyGenel.Controls.Add(MakeLabelPair(L("language_label"), cmbDil = new ComboBox { Width = 250, DropDownStyle = ComboBoxStyle.DropDownList }));
        UIHelper.StyleComboBox(cmbDil);
        for (int i = 0; i < LocalizationManager.SupportedLanguages.Length; i++) cmbDil.Items.Add(LocalizationManager.LanguageDisplayNames[i]);
        int li = Array.IndexOf(LocalizationManager.SupportedLanguages, Program.Settings.Language);
        cmbDil.SelectedIndex = li >= 0 ? li : 0;
        bodyGenel.Controls.Add(new Panel { Height = 10, Width = 10 });
        bodyGenel.Controls.Add(MakeLabelPair(L("company_name_label"), txtFirma = new TextBox { Width = 350 }));
        txtFirma.Text = Program.Settings.CompanyName; UIHelper.StyleTextBox(txtFirma);
        bodyGenel.Controls.Add(new Label { Text = L("company_name_hint"), Font = new Font("SF Pro Text", 8), ForeColor = UIHelper.TextDim, AutoSize = true, Margin = new Padding(0, -2, 0, 0) });

        tbl.Controls.Add(cardGenel, 0, 0);

        // ═══ VERİTABANI VE YEDEKLEME ═══
        FlowLayoutPanel bodyDb;
        var cardDb = UIHelper.MakeSettingsGroup(L("db_path_label"), "📂", UIHelper.AccentPurple, out bodyDb, 400);
        cardDb.Dock = DockStyle.Fill;
        
        var pnlDbPath = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        txtDbPath = new TextBox { Width = 250, ReadOnly = true, Text = Program.Settings.DbPath };
        UIHelper.StyleTextBox(txtDbPath); txtDbPath.ForeColor = UIHelper.TextDim;
        var btnDbDeg = UIHelper.MakeButton(L("change_db"), UIHelper.BtnMid, 0, 0, 90, 28);
        btnDbDeg.Click += (_, _) => { using var d = new SaveFileDialog { Title = L("db_location"), Filter = "SQLite DB|*.db", FileName = "stok.db" }; if (d.ShowDialog() == DialogResult.OK) txtDbPath.Text = d.FileName; };
        pnlDbPath.Controls.AddRange(new Control[] { txtDbPath, btnDbDeg });
        bodyDb.Controls.Add(MakeLabelPair(L("db_path_label"), pnlDbPath));

        bodyDb.Controls.Add(new Panel { Height = 10, Width = 10 });
        var btnBackupDb = UIHelper.MakeButton(L("backup_db"), UIHelper.AccentYellow, 0, 0, 240, 36);
        btnBackupDb.ForeColor = Color.Black; btnBackupDb.Click += (_, _) => BackupDb();
        bodyDb.Controls.Add(btnBackupDb);
        bodyDb.Controls.Add(new Label { Text = L("backup_db_desc"), Font = new Font("SF Pro Text", 8), ForeColor = UIHelper.TextDim, AutoSize = true, Margin = new Padding(0, 4, 0, 10) });

        var btnBackupSql = UIHelper.MakeButton(L("backup_sql"), UIHelper.AccentCyan, 0, 0, 240, 36);
        btnBackupSql.Click += (_, _) => BackupSql();
        bodyDb.Controls.Add(btnBackupSql);
        bodyDb.Controls.Add(new Label { Text = L("backup_sql_desc"), Font = new Font("SF Pro Text", 8), ForeColor = UIHelper.TextDim, AutoSize = true, Margin = new Padding(0, 4, 0, 0) });

        tbl.Controls.Add(cardDb, 1, 0);

        // ═══ GÜVENLİK ═══
        FlowLayoutPanel bodySec;
        var cardSec = UIHelper.MakeSettingsGroup(L("change_password"), "🔐", UIHelper.AccentOrange, out bodySec, 400);
        cardSec.Dock = DockStyle.Fill;
        
        var txtEski = new TextBox { Width = 360, UseSystemPasswordChar = true }; UIHelper.StyleTextBox(txtEski);
        var txtYeni = new TextBox { Width = 360, UseSystemPasswordChar = true }; UIHelper.StyleTextBox(txtYeni);
        var txtTekrar = new TextBox { Width = 360, UseSystemPasswordChar = true }; UIHelper.StyleTextBox(txtTekrar);
        
        bodySec.Controls.Add(MakeLabelPair(L("old_password"), txtEski));
        bodySec.Controls.Add(MakeLabelPair(L("new_password"), txtYeni));
        bodySec.Controls.Add(MakeLabelPair(L("confirm_password"), txtTekrar));
        
        var btnSifre = UIHelper.MakeButton(L("change_password"), UIHelper.AccentOrange, 0, 10, 200, 36);
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
        bodySec.Controls.Add(btnSifre);

        tbl.Controls.Add(cardSec, 0, 1);

        // ═══ HAKKINDA ═══
        FlowLayoutPanel bodyAbout;
        var cardAbout = UIHelper.MakeSettingsGroup(L("info"), "ℹ", UIHelper.AccentCyan, out bodyAbout, 400);
        cardAbout.Dock = DockStyle.Fill;
        
        var lblTitle = new Label { Text = "Stokendra", AutoSize = true, Font = new Font("SF Pro Display", 14, FontStyle.Bold), ForeColor = UIHelper.AccentCyan, Margin = new Padding(0, 0, 0, 4) };
        var lblVer = new Label { Text = "v3.9.1  •  Geliştirici: Tolgahan Acar", AutoSize = true, Font = new Font("SF Pro Text Semibold", 9), ForeColor = UIHelper.TextSecondary, Margin = new Padding(0, 0, 0, 10) };
        var lblDesc = new Label { Text = "Bu yazılım lisanslıdır. Tüm hakları saklıdır.", AutoSize = true, Font = new Font("SF Pro Text", 8.5f), ForeColor = UIHelper.TextDim, Margin = new Padding(0, 0, 0, 15) };
        
        var btnUpdate = UIHelper.MakeButton(L("check_updates"), UIHelper.AccentCyan, 0, 0, 200, 32);
        btnUpdate.ForeColor = Color.Black; btnUpdate.Click += async (_, _) => await CheckForUpdates();
        
        bodyAbout.Controls.AddRange(new Control[] { lblTitle, lblVer, lblDesc, btnUpdate });
        tbl.Controls.Add(cardAbout, 1, 1);

        // SAVE BUTTON AT THE BOTTOM OF TABLE
        var pnlSave = new Panel { Width = 400, Height = 100, Padding = new Padding(0, 20, 0, 0) };
        var btnKaydet = UIHelper.MakeButton(L("save_settings"), UIHelper.AccentGreen, 0, 0, 200, 44);
        btnKaydet.Font = new Font("SF Pro Display", 11, FontStyle.Bold);
        btnKaydet.Click += KaydetAyarlar;
        pnlSave.Controls.Add(btnKaydet);
        tbl.Controls.Add(pnlSave, 0, 2);
        tbl.SetColumnSpan(pnlSave, 2);

        pnlScroll.Controls.Add(tbl);
        Controls.Add(pnlScroll); Controls.Add(pnlH);
    }

    private Control MakeLabelPair(string label, Control input)
    {
        var p = new FlowLayoutPanel { Width = 360, AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0, 0, 0, 10) };
        p.Controls.Add(new Label { Text = label, AutoSize = true, Font = UIHelper.FontLabel, ForeColor = UIHelper.TextSecondary, Margin = new Padding(0, 0, 0, 4) });
        p.Controls.Add(input);
        return p;
    }

    static Label MakeLbl(string t, int x, int y) => new Label { Text = t, Left = x, Top = y + 3, Width = 190, Font = UIHelper.FontLabel, ForeColor = UIHelper.TextSecondary };
    static Panel MakeDiv(ref int y) { var p = new Panel { Left = 0, Top = y, Width = 600, Height = 1, BackColor = UIHelper.Divider }; y += 14; return p; }
    static Panel SectionHeader(string text, Color c, ref int y)
    {
        var pnl = new Panel { Left = 0, Top = y, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.Transparent };
        var lblText = new Label { Text = text, Left = 0, Top = 0, AutoSize = true, Font = new Font("SF Pro Display", 10, FontStyle.Bold), ForeColor = c };
        pnl.Controls.Add(lblText);
        y += 30;
        return pnl;
    }

    private void BackupDb()
    {
        using var dlg = new SaveFileDialog { Title = L("backup_db"), Filter = "SQLite DB|*.db", FileName = $"stokendra_yedek_{DateTime.Now:yyyyMMdd_HHmm}.db" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try { File.Copy(Program.Settings.DbPath, dlg.FileName, true); MessageBox.Show(L("backup_success", dlg.FileName), L("info"), MessageBoxButtons.OK, MessageBoxIcon.Information); }
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

            using var con = new SqliteConnection($"Data Source={Program.Settings.DbPath}");
            con.Open();
            string[] tables = { "StokKartlari", "StokHareketleri", "Notlar", "Birimler", "Departmanlar", "AppConfig", "AuditLog", "Kullanicilar", "ServisKayitlari" };
            foreach (var table in tables)
            {
                try
                {
                    using var cmd = con.CreateCommand();
                    cmd.CommandText = $"SELECT sql FROM sqlite_master WHERE type='table' AND name='{table}'";
                    var createSql = cmd.ExecuteScalar()?.ToString();
                    if (!string.IsNullOrEmpty(createSql)) { sb.AppendLine($"DROP TABLE IF EXISTS {table};"); sb.AppendLine(createSql + ";"); }

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
                }
                catch { }
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
    
    private async Task CheckForUpdates()
    {
        Cursor.Current = Cursors.WaitCursor;
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Stokendra-App");
            string url = "https://api.github.com/repos/tolgahanacar/Stokendra/releases/latest";
            var response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(jsonString);
                string latestVersion = jsonDoc.RootElement.GetProperty("tag_name").GetString() ?? "";
                string htmlUrl = jsonDoc.RootElement.GetProperty("html_url").GetString() ?? "";
                string currentVersion = "v" + Application.ProductVersion;
                if (string.Compare(latestVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    if (MessageBox.Show(L("update_available", latestVersion, currentVersion), L("update_title"), MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    { Process.Start(new ProcessStartInfo { FileName = htmlUrl, UseShellExecute = true }); }
                }
                else MessageBox.Show(L("up_to_date", currentVersion), L("info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex) { MessageBox.Show(L("update_error", ex.Message), L("error"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { Cursor.Current = Cursors.Default; }
    }
}
