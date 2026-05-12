namespace SpaceWallpaperApp;

public partial class Form1 : Form
{
    private readonly NasaImageService _nasaImageService = new();
    private readonly WallpaperService _wallpaperService = new();
    private readonly AutoStartService _autoStartService = new();
    private readonly AppSettingsStore _settingsStore = new();
    private readonly string _downloadDirectory;
    private readonly string _generatedWallpaperDirectory;
    private readonly bool _startupMode;

    private ComboBox _styleComboBox = null!;
    private Button _refreshButton = null!;
    private Button _applyButton = null!;
    private PictureBox _previewBox = null!;
    private Panel _previewCaptionPanel = null!;
    private Label _previewCaptionLabel = null!;
    private Label _titleLabel = null!;
    private Label _detailsLabel = null!;
    private Label _statusLabel = null!;
    private LinkLabel _sourceLink = null!;
    private CheckBox _autoStartCheckBox = null!;

    private string? _currentImagePath;
    private NasaImageCandidate? _currentCandidate;
    private AppSettings _settings = null!;

    public Form1(bool startupMode)
    {
        _startupMode = startupMode;
        _downloadDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpaceWallpaperApp",
            "Wallpapers");
        _generatedWallpaperDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpaceWallpaperApp",
            "Generated");
        _settings = _settingsStore.Load();
        MigrateLegacyStyleDefault();

        InitializeComponent();
        BuildLayout();
    }

    private void BuildLayout()
    {
        SuspendLayout();

        Text = "Space Wallpaper";
        MinimumSize = new Size(1080, 720);
        StartPosition = FormStartPosition.CenterScreen;

        if (_startupMode)
        {
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            Opacity = 0;
        }

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 60,
            Padding = new Padding(20, 16, 20, 0),
            Font = new Font("Segoe UI Semibold", 19F, FontStyle.Bold),
            Text = "Real NASA space wallpaper",
        };

        var subtitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(20, 0, 20, 8),
            ForeColor = Color.DimGray,
            Text = "Fetch a real high-resolution image from NASA, preview it, and apply it as your Windows background.",
        };

        var controlsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 88,
            Padding = new Padding(20, 8, 20, 8),
            ColumnCount = 6,
            RowCount = 1,
        };
        controlsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        controlsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        controlsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        controlsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        controlsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        controlsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10));

        controlsPanel.Controls.Add(new Label
        {
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            Text = "Style",
        }, 0, 0);

        _styleComboBox = new ComboBox
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _styleComboBox.Items.AddRange(Enum.GetNames<WallpaperStyle>());
        controlsPanel.Controls.Add(_styleComboBox, 1, 0);

        _autoStartCheckBox = new CheckBox
        {
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            Text = "Random wallpaper on Windows startup",
        };
        _autoStartCheckBox.CheckedChanged += AutoStartCheckBox_CheckedChanged;
        controlsPanel.Controls.Add(_autoStartCheckBox, 2, 0);

        _refreshButton = new Button
        {
            Anchor = AnchorStyles.Right,
            AutoSize = false,
            Padding = new Padding(14, 8, 14, 8),
            Size = new Size(135, 36),
            Text = "Fetch New Image",
            UseVisualStyleBackColor = true,
        };
        _refreshButton.Click += RefreshButton_Click;
        controlsPanel.Controls.Add(_refreshButton, 3, 0);

        _applyButton = new Button
        {
            Anchor = AnchorStyles.Right,
            AutoSize = false,
            Padding = new Padding(14, 8, 14, 8),
            Size = new Size(125, 36),
            Text = "Set Wallpaper",
            UseVisualStyleBackColor = true,
        };
        _applyButton.Click += ApplyButton_Click;
        controlsPanel.Controls.Add(_applyButton, 4, 0);

        var contentSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 0, 20, 20),
            SplitterDistance = 700,
            FixedPanel = FixedPanel.Panel2,
        };

        var previewPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            BorderStyle = BorderStyle.FixedSingle,
        };

        _previewBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            SizeMode = PictureBoxSizeMode.Zoom,
        };

        _previewCaptionPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = Color.FromArgb(28, 28, 28),
            Padding = new Padding(12, 10, 12, 10),
        };

        _previewCaptionLabel = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.WhiteSmoke,
            TextAlign = ContentAlignment.TopLeft,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            Text = "Image description will appear here.",
        };

        _previewCaptionPanel.Controls.Add(_previewCaptionLabel);
        previewPanel.Controls.Add(_previewCaptionPanel);
        previewPanel.Controls.Add(_previewBox);
        contentSplit.Panel1.Controls.Add(previewPanel);

        var infoPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(16),
        };
        infoPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        infoPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        infoPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        infoPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        infoPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        infoPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _titleLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold),
            Text = "No image loaded yet",
        };

        _detailsLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.DimGray,
            MaximumSize = new Size(640, 0),
            Text = "Choose a topic and fetch a high-resolution space image.",
        };

        _sourceLink = new LinkLabel
        {
            AutoSize = true,
            Text = "NASA source",
            Visible = false,
        };
        _sourceLink.LinkClicked += (_, _) =>
        {
            if (_currentCandidate?.NasaDetailsUrl is not null)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _currentCandidate.NasaDetailsUrl,
                    UseShellExecute = true,
                });
            }
        };

        var noteLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(320, 0),
            Text = "The app uses NASA's public image library to fetch random space images.",
        };

        _statusLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.DarkSlateBlue,
            MaximumSize = new Size(320, 0),
            Text = "Ready.",
        };

        infoPanel.Controls.Add(_titleLabel);
        infoPanel.Controls.Add(_detailsLabel);
        infoPanel.Controls.Add(_sourceLink);
        infoPanel.Controls.Add(noteLabel);
        infoPanel.Controls.Add(new Panel());
        infoPanel.Controls.Add(_statusLabel);
        contentSplit.Panel2.Controls.Add(infoPanel);

        Controls.Add(contentSplit);
        Controls.Add(controlsPanel);
        Controls.Add(subtitle);
        Controls.Add(title);

        ApplySettingsToUi();
        ResumeLayout();
    }

    private void UpdatePreviewCaption(string text)
    {
        _previewCaptionLabel.Text = text;

        using var g = _previewCaptionLabel.CreateGraphics();
        var availableWidth = _previewCaptionPanel.ClientSize.Width - _previewCaptionPanel.Padding.Horizontal;
        if (availableWidth <= 0) return;

        var measuredSize = g.MeasureString(text, _previewCaptionLabel.Font, availableWidth);
        var desiredHeight = (int)Math.Ceiling(measuredSize.Height) + _previewCaptionPanel.Padding.Vertical;
        var clampedHeight = Math.Max(40, Math.Min(desiredHeight, 150));

        _previewCaptionPanel.Height = clampedHeight;
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_startupMode)
        {
            await RunStartupAutomationAsync();
            Close();
            return;
        }

        await LoadNewImageAsync();
    }

    private async void RefreshButton_Click(object? sender, EventArgs e)
    {
        await LoadNewImageAsync();
    }

    private async void ApplyButton_Click(object? sender, EventArgs e)
    {
        if (_currentImagePath is null || _currentCandidate is null)
        {
            MessageBox.Show(this, "Fetch an image first.", "No image", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            ToggleBusyState(true, "Applying wallpaper...");
            var selectedStyle = Enum.Parse<WallpaperStyle>(_styleComboBox.SelectedItem!.ToString()!);
            await Task.Run(() =>
            {
                var renderedWallpaperPath = _wallpaperService.CreateCaptionedWallpaper(_currentImagePath, _currentCandidate, _generatedWallpaperDirectory, selectedStyle);
                _wallpaperService.SetWallpaper(renderedWallpaperPath, selectedStyle);
            });
            SaveCurrentSelections();
            SetStatus("Wallpaper updated successfully.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to set wallpaper.{Environment.NewLine}{Environment.NewLine}{ex.Message}", "Wallpaper error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("Wallpaper update failed.");
        }
        finally
        {
            ToggleBusyState(false);
        }
    }

    private async Task LoadNewImageAsync()
    {
        try
        {
            ToggleBusyState(true, "Searching NASA image library...");

            var candidate = await _nasaImageService.GetHighResolutionImageAsync(_downloadDirectory, CancellationToken.None);
            _currentCandidate = candidate;
            _currentImagePath = candidate.LocalPath;
            SaveCurrentSelections();

            ReplacePreview(candidate.LocalPath);
            _titleLabel.Text = candidate.Title;
            _detailsLabel.Text = $"{candidate.Width} x {candidate.Height}px{Environment.NewLine}{candidate.Description}";
            UpdatePreviewCaption(BuildPreviewCaption(candidate));
            _sourceLink.Visible = true;
            SetStatus($"Loaded \"{candidate.Title}\".");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not load a wallpaper image.{Environment.NewLine}{Environment.NewLine}{ex.Message}", "NASA download error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("Image fetch failed.");
            UpdatePreviewCaption("Image description will appear here.");
        }
        finally
        {
            ToggleBusyState(false);
        }
    }

    private void ReplacePreview(string imagePath)
    {
        var previousImage = _previewBox.Image;
        using var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var image = Image.FromStream(fs);
        _previewBox.Image = new Bitmap(image);
        previousImage?.Dispose();
    }

    private void ToggleBusyState(bool isBusy, string? status = null)
    {
        _refreshButton.Enabled = !isBusy;
        _applyButton.Enabled = !isBusy;
        _styleComboBox.Enabled = !isBusy;
        _autoStartCheckBox.Enabled = !isBusy;
        UseWaitCursor = isBusy;

        if (status is not null)
        {
            SetStatus(status);
        }
    }

    private void SetStatus(string text)
    {
        _statusLabel.Text = text;
    }

    private static string BuildPreviewCaption(NasaImageCandidate candidate)
    {
        return $"{candidate.Title}{Environment.NewLine}{candidate.Description.Trim()}";
    }

    private void ApplySettingsToUi()
    {
        var styleName = Enum.GetNames<WallpaperStyle>().Contains(_settings.SelectedStyle)
            ? _settings.SelectedStyle
            : WallpaperStyle.Fit.ToString();
        _styleComboBox.SelectedItem = styleName;

        _autoStartCheckBox.Checked = _autoStartService.IsEnabled();
    }

    private void SaveCurrentSelections()
    {
        _settings.SelectedStyle = _styleComboBox.SelectedItem?.ToString() ?? WallpaperStyle.Fit.ToString();
        _settingsStore.Save(_settings);
    }

    private void AutoStartCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_startupMode)
        {
            return;
        }

        try
        {
            if (_autoStartCheckBox.Checked)
            {
                _autoStartService.Enable();
                _settings.AutoStartEnabled = true;
                SetStatus("Daily startup wallpaper is enabled.");
            }
            else
            {
                _autoStartService.Disable();
                _settings.AutoStartEnabled = false;
                SetStatus("Daily startup wallpaper is disabled.");
            }

            SaveCurrentSelections();
        }
        catch (Exception ex)
        {
            _autoStartCheckBox.CheckedChanged -= AutoStartCheckBox_CheckedChanged;
            _autoStartCheckBox.Checked = _autoStartService.IsEnabled();
            _autoStartCheckBox.CheckedChanged += AutoStartCheckBox_CheckedChanged;

            MessageBox.Show(this, $"Could not update Windows startup settings.{Environment.NewLine}{Environment.NewLine}{ex.Message}", "Startup settings error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task RunStartupAutomationAsync()
    {
        if (!_autoStartService.IsEnabled())
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        if (_settings.LastAutoAppliedDate == today)
        {
            return;
        }

        try
        {
            var candidate = await _nasaImageService.GetHighResolutionImageAsync(_downloadDirectory, CancellationToken.None);
            var style = Enum.TryParse<WallpaperStyle>(_settings.SelectedStyle, out var parsedStyle)
                ? parsedStyle
                : WallpaperStyle.Fit;

            var renderedWallpaperPath = _wallpaperService.CreateCaptionedWallpaper(candidate.LocalPath, candidate, _generatedWallpaperDirectory, style);
            _wallpaperService.SetWallpaper(renderedWallpaperPath, style);
            _settings.LastAutoAppliedDate = today;
            _settingsStore.Save(_settings);
        }
        catch
        {
            // Silent failure in startup mode to avoid interrupting sign-in.
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _previewBox?.Image?.Dispose();
        base.OnFormClosed(e);
    }

    private void MigrateLegacyStyleDefault()
    {
        if (_settings.HasMigratedToFitDefault)
        {
            return;
        }

        if (string.Equals(_settings.SelectedStyle, WallpaperStyle.Fill.ToString(), StringComparison.Ordinal))
        {
            _settings.SelectedStyle = WallpaperStyle.Fit.ToString();
        }

        _settings.HasMigratedToFitDefault = true;
        _settingsStore.Save(_settings);
    }
}
