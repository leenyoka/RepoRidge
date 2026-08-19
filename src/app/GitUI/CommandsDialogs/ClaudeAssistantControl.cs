using System.Diagnostics;
using System.Text;
using GitExtUtils.GitUI.Theming;
using GitUI.Theming;
using GitUIPluginInterfaces;

namespace GitUI.CommandsDialogs;

/// <summary>
/// Chat panel that sends prompts to the Claude CLI and displays responses inline.
/// Requires the Claude Code CLI ("claude") to be installed and logged in.
/// </summary>
public sealed class ClaudeAssistantControl : UserControl
{
    private readonly RichTextBox _conversationBox;
    private readonly TextBox _inputBox;
    private readonly Button _sendButton;
    private readonly CheckBox _includeContextCheckBox;
    private readonly Label _statusLabel;
    private GitRevision? _currentRevision;
    private string? _workingDir;
    private bool _isBusy;

    public ClaudeAssistantControl()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(6);

        _conversationBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            Font = new Font("Segoe UI", 9.5f),
            DetectUrls = false,
        };

        _inputBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Segoe UI", 9.5f),
            PlaceholderText = "Ask Claude about this commit, diff, or repository… (Ctrl+Enter to send)",
        };

        _sendButton = new Button
        {
            Text = "Ask Claude",
            Width = 110,
            Dock = DockStyle.Right,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        };

        _includeContextCheckBox = new CheckBox
        {
            Text = "Include commit context",
            Checked = true,
            Dock = DockStyle.Left,
            AutoSize = true,
        };

        _statusLabel = new Label
        {
            Text = string.Empty,
            Dock = DockStyle.Left,
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
            Padding = new Padding(6, 0, 0, 0),
        };

        // Bottom action bar: [context checkbox] [status] ... [send button]
        Panel bottomBar = new()
        {
            Dock = DockStyle.Bottom,
            Height = 34,
            Padding = new Padding(0, 4, 0, 0),
        };
        bottomBar.Controls.Add(_sendButton);
        bottomBar.Controls.Add(_statusLabel);
        bottomBar.Controls.Add(_includeContextCheckBox);

        // Input panel wraps the text box
        Panel inputPanel = new()
        {
            Dock = DockStyle.Bottom,
            Height = 82,
            Padding = new Padding(0, 4, 0, 0),
        };
        inputPanel.Controls.Add(_inputBox);

        // Add in order: Fill first, then Bottom panels (bottom-most last)
        Controls.Add(_conversationBox);
        Controls.Add(inputPanel);
        Controls.Add(bottomBar);

        ThemeModule.ThemeChanged += OnThemeChanged;
        Disposed += (_, _) => ThemeModule.ThemeChanged -= OnThemeChanged;

        ApplyThemeColors();

        _sendButton.Click += OnSendClick;
        _inputBox.KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                OnSendClick(null, EventArgs.Empty);
            }
        };

        AppendMessage("Claude", "Hello! Select a commit and ask me anything — explain a diff, summarise changes, suggest a fix, or ask about the repo.");
    }

    public void SetContext(GitRevision? revision, string? workingDir)
    {
        _currentRevision = revision;
        _workingDir = workingDir;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ApplyThemeColors();

    private void ApplyThemeColors()
    {
        _conversationBox.BackColor = AppColor.EditorBackground.GetThemeColor() is { IsEmpty: false } c ? c : SystemColors.Window;
        _conversationBox.ForeColor = SystemColors.WindowText;
        _inputBox.BackColor = AppColor.EditorBackground.GetThemeColor() is { IsEmpty: false } ic ? ic : SystemColors.Window;
        _inputBox.ForeColor = SystemColors.WindowText;
    }

    private void OnSendClick(object? sender, EventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        string userInput = _inputBox.Text.Trim();
        if (string.IsNullOrEmpty(userInput))
        {
            return;
        }

        _inputBox.Clear();
        AppendMessage("You", userInput);
        AskClaude(userInput);
    }

    private void AskClaude(string userInput)
    {
        _isBusy = true;
        _sendButton.Enabled = false;
        _statusLabel.Text = "Asking Claude…";

        string prompt = BuildPrompt(userInput);

        Task.Run(() =>
        {
            string response = RunClaudeCli(prompt);
            BeginInvoke(() =>
            {
                AppendMessage("Claude", response);
                _statusLabel.Text = string.Empty;
                _sendButton.Enabled = true;
                _isBusy = false;
            });
        });
    }

    private string BuildPrompt(string userInput)
    {
        if (!_includeContextCheckBox.Checked || (_currentRevision is null && _workingDir is null))
        {
            return userInput;
        }

        StringBuilder sb = new();
        sb.AppendLine("You are a git assistant. Answer concisely. Context:");

        if (!string.IsNullOrEmpty(_workingDir))
        {
            sb.AppendLine($"Repository: {_workingDir}");
        }

        if (_currentRevision is not null)
        {
            sb.AppendLine($"Commit: {_currentRevision.ObjectId.ToShortString()} — {_currentRevision.Subject}");
            sb.AppendLine($"Author: {_currentRevision.Author}  Date: {_currentRevision.CommitDate:yyyy-MM-dd}");
        }

        sb.AppendLine();
        sb.AppendLine("Question:");
        sb.Append(userInput);
        return sb.ToString();
    }

    private static string RunClaudeCli(string prompt)
    {
        try
        {
            using Process process = new();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "claude",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            process.StartInfo.ArgumentList.Add("-p");
            process.StartInfo.ArgumentList.Add(prompt);

            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit(120_000);

            if (!string.IsNullOrWhiteSpace(output))
            {
                return output.Trim();
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                return $"Claude error: {error.Trim()}";
            }

            return "No response received. Make sure the Claude CLI is installed and you are logged in (run: claude login).";
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return "Claude CLI not found in PATH.\n\nInstall Claude Code from claude.ai/code, then log in by running: claude login";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private void AppendMessage(string speaker, string message)
    {
        if (_conversationBox.TextLength > 0)
        {
            _conversationBox.AppendText(Environment.NewLine + Environment.NewLine);
        }

        // Speaker label in bold
        int startIndex = _conversationBox.TextLength;
        _conversationBox.AppendText($"{speaker}:");
        _conversationBox.Select(startIndex, speaker.Length + 1);
        _conversationBox.SelectionFont = new Font(_conversationBox.Font, FontStyle.Bold);
        _conversationBox.SelectionColor = speaker == "You" ? Color.FromArgb(79, 195, 247) : Color.FromArgb(165, 214, 167);
        _conversationBox.Select(_conversationBox.TextLength, 0);
        _conversationBox.SelectionFont = _conversationBox.Font;
        _conversationBox.SelectionColor = _conversationBox.ForeColor;

        _conversationBox.AppendText(Environment.NewLine + message);
        _conversationBox.SelectionStart = _conversationBox.TextLength;
        _conversationBox.ScrollToCaret();
    }
}
