using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using GitCommands.Settings;
using GitCommands.UserRepositoryHistory;
using GitCommands.Utils;
using Microsoft.WindowsAPICodePack.Shell;
using Microsoft.WindowsAPICodePack.Taskbar;

namespace GitUI;

public interface IWindowsJumpListManager : IDisposable
{
    bool NeedsJumpListCreation { get; }

    void AddToRecent(string workingDir);
    void CreateJumpList(IntPtr windowHandle, WindowsThumbnailToolbarButtons buttons);
    void EnableThumbnailToolbar(bool enable);
    void UpdateCommitIcon(Image image);
}

/// <summary>
///     Provides access to Windows taskbar jumplists features.
/// </summary>
/// <seealso href="https://www.sevenforums.com/news/44368-developing-windows-7-taskbar-thumbnail-toolbars.html" />
/// <seealso
///     href="https://github.com/jlnewton87/Programming/blob/master/C%23/Windows%20API%20Code%20Pack%201.1/source/WindowsAPICodePack/Shell/Taskbar/JumpList.cs" />
/// <inheritdoc />
public sealed class WindowsJumpListManager(IRepositoryDescriptionProvider repositoryDescriptionProvider)
    : IWindowsJumpListManager
{
    private static readonly Dictionary<Image, Icon> _iconByImage = [];
    private ThumbnailToolBarButton? _closeAllButton;
    private ThumbnailToolBarButton? _commitButton;
    private string? _deferredAddToRecent;
    private ThumbnailToolBarButton? _pullButton;
    private ThumbnailToolBarButton? _pushButton;

    static WindowsJumpListManager()
    {
        if (TaskbarManager.IsPlatformSupported)
        {
            var id = AppSettings.ApplicationId;
            TaskbarManager.Instance.ApplicationId = AppSettings.IsPortable()
                ? $"{id}.{Convert.ToBase64String(SHA1.HashData(Encoding.UTF8.GetBytes(Application.ExecutablePath)))}"
                : id;
        }
    }

    private bool ToolbarButtonsCreated
    {
        get => _commitButton is not null;
    }

    private static bool IsSupported
    {
        get => OperatingSystem.IsWindows() && TaskbarManager.IsPlatformSupported;
    }

    private static bool IsSupportedAndVisible
    {
        get => EnvUtils.RunningOnWindowsWithMainWindow() && TaskbarManager.IsPlatformSupported;
    }

    // MEF will dispose instantiated parts when the container is disposed. There is no need to include a finalizer here.
    void IDisposable.Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Adds the given working directory to the list of Recent for future quick access.
    /// </summary>
    void IWindowsJumpListManager.AddToRecent(string workingDir)
    {
        if (!IsSupported)
        {
            return;
        }

        if (!ToolbarButtonsCreated)
        {
            _deferredAddToRecent = workingDir;
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(workingDir);

        SafeInvoke(() =>
        {
            var repositoryDescription = repositoryDescriptionProvider.GetDescriptiveUnique(workingDir);
            if (string.IsNullOrWhiteSpace(repositoryDescription))
            {
                return;
            }

            var baseFolder = Path.Join(AppSettings.ApplicationDataPath.Value!, "Recent");
            if (!Directory.Exists(baseFolder))
            {
                Directory.CreateDirectory(baseFolder);
            }

            // sanitise
            StringBuilder sb = new(repositoryDescription);
            sb.Replace(@"\\wsl$\", "").Replace(@":\", "_");
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                sb.Replace(c, '_');
            }

            var path = Path.Join(baseFolder, $"{sb}.gitext");
            File.WriteAllText(path, workingDir);
            JumpList.AddToRecent(path);
            UpdateJumpList(); // in order to refresh at once

            if (!ToolbarButtonsCreated)
            {
                return;
            }

            Validates.NotNull(_closeAllButton);
            Validates.NotNull(_commitButton);
            Validates.NotNull(_pushButton);
            Validates.NotNull(_pullButton);

            _closeAllButton.Enabled = true;
            _commitButton.Enabled = true;
            _pushButton.Enabled = true;
            _pullButton.Enabled = true;
        }, nameof(AddToRecent));
    }

    void IWindowsJumpListManager.UpdateCommitIcon(Image image)
    {
        SafeInvoke(() =>
        {
            if (ToolbarButtonsCreated && IsSupportedAndVisible)
            {
                Validates.NotNull(_commitButton);
                _commitButton.Icon = MakeIcon(image, 48, true);
            }
        }, nameof(UpdateCommitIcon));
    }

    /// <summary>
    ///     Indicates if the JumpList creation is still needed.
    /// </summary>
    bool IWindowsJumpListManager.NeedsJumpListCreation
    {
        get => IsSupported && !ToolbarButtonsCreated;
    }

    /// <summary>
    ///     Creates a JumpList for the given application instance.
    ///     It also adds thumbnail toolbars, which are a set of up to seven buttons at the bottom of the taskbar’s icon
    ///     thumbnail preview.
    /// </summary>
    /// <param name="windowHandle">The application instance's main window handle.</param>
    /// <param name="buttons">The thumbnail toolbar buttons to be added.</param>
    void IWindowsJumpListManager.CreateJumpList(IntPtr windowHandle, WindowsThumbnailToolbarButtons buttons)
    {
        if (ToolbarButtonsCreated || !IsSupported || windowHandle == IntPtr.Zero)
        {
            return;
        }

        SafeInvoke(() =>
        {
            UpdateJumpList();

            CreateTaskbarButtons(windowHandle, buttons);
        }, nameof(CreateJumpList));

        if (ToolbarButtonsCreated && _deferredAddToRecent is not null)
        {
            var recentRepoAddToRecent = _deferredAddToRecent;
            _deferredAddToRecent = null;
            AddToRecent(recentRepoAddToRecent);
        }

        return;

        void CreateTaskbarButtons(IntPtr handle, WindowsThumbnailToolbarButtons thumbButtons)
        {
            _commitButton =
                new ThumbnailToolBarButton(MakeIcon(thumbButtons.Commit.Image, 48, true), thumbButtons.Commit.Text);
            _commitButton.Click += thumbButtons.Commit.Click;

            _pushButton =
                new ThumbnailToolBarButton(MakeIcon(thumbButtons.Push.Image, 48, true), thumbButtons.Push.Text);
            _pushButton.Click += thumbButtons.Push.Click;

            _pullButton =
                new ThumbnailToolBarButton(MakeIcon(thumbButtons.Pull.Image, 48, true), thumbButtons.Pull.Text);
            _pullButton.Click += thumbButtons.Pull.Click;

            _closeAllButton = new ThumbnailToolBarButton(MakeIcon(thumbButtons.CloseAll.Image, 48, true),
                thumbButtons.CloseAll.Text);
            _closeAllButton.Click += thumbButtons.CloseAll.Click;

            // Call this method using reflection.  This is a workaround to *not* reference WPF libraries, because of how the WindowsAPICodePack was implemented.
            TaskbarManager.Instance.ThumbnailToolBars.AddButtons(handle, _commitButton, _pullButton, _pushButton,
                _closeAllButton);
        }
    }

    /// <summary>
    ///     Sets the enabled state of thumbnail toolbar buttons.
    /// </summary>
    void IWindowsJumpListManager.EnableThumbnailToolbar(bool enable)
    {
        if (!ToolbarButtonsCreated)
        {
            return;
        }

        SafeInvoke(() =>
        {
            Validates.NotNull(_closeAllButton);
            Validates.NotNull(_commitButton);
            Validates.NotNull(_pushButton);
            Validates.NotNull(_pullButton);

            // _closeAllButton is applicable always

            _commitButton.Enabled = enable;
            _pushButton.Enabled = enable;
            _pullButton.Enabled = enable;
        }, nameof(EnableThumbnailToolbar));
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _closeAllButton?.Dispose();
            _commitButton?.Dispose();
            _pushButton?.Dispose();
            _pullButton?.Dispose();
        }
    }

    /// <summary>
    ///     Updates the jump list to show recent repositories in the Start menu and at the taskbar icon.
    ///     Uses the built-in Windows localized "Recent" category.
    /// </summary>
    private static void UpdateJumpList()
    {
        var jumpList = JumpList.CreateJumpList();
        jumpList.ClearAllUserTasks();
        jumpList.KnownCategoryToDisplay = JumpListKnownCategoryType.Recent;
        jumpList.Refresh();
    }

    /// <summary>
    ///     Converts an image into an icon.  This was taken off of the interwebs.
    ///     It's on a billion different sites and forum posts, so I would say its creative commons by now. -tekmaven.
    /// </summary>
    /// <param name="img">The image that shall become an icon.</param>
    /// <param name="size">
    ///     The width and height of the icon. Standard
    ///     sizes are 16x16, 32x32, 48x48, 64x64.
    /// </param>
    /// <param name="keepAspectRatio">
    ///     Whether the image should be squashed into a
    ///     square or whether whitespace should be put around it.
    /// </param>
    /// <returns>An icon!!.</returns>
    private static Icon MakeIcon(Image img, int size, bool keepAspectRatio)
    {
        if (_iconByImage.TryGetValue(img, out var icon))
        {
            return icon;
        }

        using Bitmap square = new(size, size); // create new bitmap
        using var g = Graphics.FromImage(square); // allow drawing to it

        int x, y, w, h; // dimensions for new image

        if (!keepAspectRatio || img.Height == img.Width)
        {
            // just fill the square
            x = y = 0; // set x and y to 0
            w = h = size; // set width and height to size
        }
        else
        {
            // work out the aspect ratio
            var r = img.Width / (float)img.Height;

            // set dimensions accordingly to fit inside size^2 square
            if (r > 1)
            {
                // w is bigger, so divide h by r
                w = size;
                h = (int)(size / r);
                x = 0;
                y = (size - h) / 2; // center the image
            }
            else
            {
                // h is bigger, so multiply w by r
                w = (int)(size * r);
                h = size;
                y = 0;
                x = (size - w) / 2; // center the image
            }
        }

        // make the image shrink nicely by using HighQualityBicubic mode
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(img, x, y, w, h); // draw image with specified dimensions
        g.Flush(); // make sure all drawing operations complete before we get the icon

        // following line would work directly on any image, but then
        // it wouldn't look as nice.
        icon = square.ToIcon();
        _iconByImage.Add(img, icon);
        return icon;
    }

    private static void SafeInvoke(Action action, string callerName)
    {
        try
        {
            action();
        }
        catch (Exception ex)
            when (
                // reported in https://github.com/gitextensions/gitextensions/issues/6760
                // reported in https://github.com/gitextensions/gitextensions/issues/8234
                ex is ShellException ||

                // reported in https://github.com/gitextensions/gitextensions/issues/2269
                ex is COMException ||

                // reported in https://github.com/gitextensions/gitextensions/issues/6767
                ex is UnauthorizedAccessException ||

                // reported in https://github.com/gitextensions/gitextensions/issues/4549
                // looks like a regression in Windows 10.0.16299 (1709)
                ex is IOException ||

                // observed during integration tests: A valid active Window is needed to update the Taskbar.
                ex is InvalidOperationException)
        {
            Trace.WriteLine(ex.Message, callerName);
        }
    }
}
