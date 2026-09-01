using Scada.Core.Product.Licensing;

namespace EliteSCADA.LicenseGenerator;

internal sealed class LicenseGeneratorForm : Form
{
    private readonly TextBox _requestCode = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 78 };
    private readonly ComboBox _tier = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _privateKeyPath = new();
    private readonly TextBox _keyId = new() { Text = "preview-1" };
    private readonly TextBox _licenseId = new() { Text = Guid.NewGuid().ToString("D") };
    private readonly DateTimePicker _expires = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm 'UTC'", ShowCheckBox = true, Checked = false };
    private readonly TextBox _outputPath = new();
    private readonly TextBox _status = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Height = 112 };
    private readonly Button _generate = new() { Text = "Gerar licença", AutoSize = true, Padding = new Padding(18, 7, 18, 7) };

    public LicenseGeneratorForm()
    {
        Text = "EliteSCADA — Gerador de Licenças";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(720, 650);
        Size = new Size(820, 720);
        AutoScaleMode = AutoScaleMode.Dpi;

        _tier.Items.AddRange(["500", "1000", "1500", "3000", "5000", "Ilimitado"]);
        _tier.SelectedIndex = 0;
        _expires.Value = DateTime.Today.AddYears(1).ToUniversalTime();
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        _outputPath.Text = Path.Combine(string.IsNullOrWhiteSpace(desktop) ? Environment.CurrentDirectory : desktop, "EliteSCADA.license");

        Controls.Add(BuildLayout());
        AcceptButton = _generate;
        _generate.Click += (_, _) => GenerateLicense();
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 4
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var title = new Label
        {
            AutoSize = true,
            Text = "Gerador offline de licenças EliteSCADA",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 6)
        };
        var explanation = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(740, 0),
            Text = "Cole o código de solicitação da máquina, selecione o limite de TAGs e informe a chave privada externa. A chave não é incorporada ao executável nem ao arquivo de licença.",
            Margin = new Padding(0, 0, 0, 16)
        };

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        AddField(fields, "Código da máquina", _requestCode);
        AddField(fields, "Limite de TAGs", _tier);
        AddField(fields, "Chave privada (.pem)", _privateKeyPath, BrowseButton("Procurar…", SelectPrivateKey));
        AddField(fields, "Identificador da chave", _keyId);
        AddField(fields, "Identificador da licença", _licenseId, new Button { Text = "Novo UUID", AutoSize = true });
        ((Button)fields.GetControlFromPosition(2, 4)!).Click += (_, _) => _licenseId.Text = Guid.NewGuid().ToString("D");
        AddField(fields, "Expiração opcional", _expires);
        AddField(fields, "Salvar licença em", _outputPath, BrowseButton("Escolher…", SelectOutputPath));

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(170, 14, 0, 10)
        };
        actions.Controls.Add(_generate);

        var statusPanel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusPanel.Controls.Add(new Label { Text = "Resultado", AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
        _status.Dock = DockStyle.Top;
        statusPanel.Controls.Add(_status);

        root.Controls.Add(title);
        root.Controls.Add(explanation);
        root.Controls.Add(fields);
        root.Controls.Add(actions);
        root.Controls.Add(statusPanel);
        return root;
    }

    private static void AddField(TableLayoutPanel layout, string label, Control editor, Control? action = null)
    {
        var row = layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var caption = new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 12, 8)
        };
        editor.Dock = DockStyle.Top;
        editor.Margin = new Padding(0, 4, 8, 4);
        layout.Controls.Add(caption, 0, row);
        layout.Controls.Add(editor, 1, row);
        if (action is not null)
        {
            action.Anchor = AnchorStyles.Left;
            action.Margin = new Padding(0, 4, 0, 4);
            layout.Controls.Add(action, 2, row);
        }
    }

    private static Button BrowseButton(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += (_, _) => action();
        return button;
    }

    private void SelectPrivateKey()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Selecionar chave privada de assinatura",
            Filter = "Chave PEM (*.pem)|*.pem|Todos os arquivos (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) _privateKeyPath.Text = dialog.FileName;
    }

    private void SelectOutputPath()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Salvar licença EliteSCADA",
            Filter = "Licença EliteSCADA (*.license)|*.license|Todos os arquivos (*.*)|*.*",
            DefaultExt = "license",
            AddExtension = true,
            FileName = Path.GetFileName(_outputPath.Text)
        };
        var currentDirectory = Path.GetDirectoryName(_outputPath.Text);
        if (!string.IsNullOrWhiteSpace(currentDirectory) && Directory.Exists(currentDirectory))
            dialog.InitialDirectory = currentDirectory;
        if (dialog.ShowDialog(this) == DialogResult.OK) _outputPath.Text = dialog.FileName;
    }

    private void GenerateLicense()
    {
        _generate.Enabled = false;
        UseWaitCursor = true;
        _status.Clear();
        try
        {
            var result = LicenseGenerationService.Generate(new LicenseGenerationRequest(
                _requestCode.Text,
                SelectedTier(),
                _privateKeyPath.Text,
                _keyId.Text,
                _outputPath.Text,
                _licenseId.Text,
                _expires.Checked ? new DateTimeOffset(DateTime.SpecifyKind(_expires.Value, DateTimeKind.Utc)) : null));

            _status.Text = string.Join(Environment.NewLine,
            [
                "Licença gerada com sucesso.",
                $"ID: {result.LicenseId}",
                $"Máquina: {result.MachineFingerprint}",
                $"Limite: {LicensingPolicy.TierDisplayName(result.Tier)}",
                $"Chave: {result.KeyId}",
                $"Expira: {(result.NotAfterUtc is null ? "Nunca" : result.NotAfterUtc.Value.ToString("u"))}",
                $"Arquivo: {result.OutputPath}"
            ]);
            MessageBox.Show(this, "A licença foi gerada e salva.", "EliteSCADA", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception error)
        {
            _status.Text = $"Falha ao gerar licença:{Environment.NewLine}{error.Message}";
            MessageBox.Show(this, error.Message, "Falha ao gerar licença", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _generate.Enabled = true;
        }
    }

    private LicenseTier SelectedTier() => _tier.SelectedItem?.ToString() switch
    {
        "500" => LicenseTier.Tags500,
        "1000" => LicenseTier.Tags1000,
        "1500" => LicenseTier.Tags1500,
        "3000" => LicenseTier.Tags3000,
        "5000" => LicenseTier.Tags5000,
        "Ilimitado" => LicenseTier.Unlimited,
        _ => throw new InvalidOperationException("Selecione um limite de TAGs.")
    };
}
