using System.IO.Abstractions;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitCommands;

public interface ICommitTemplateManager
{
    /// <summary>
    ///     Gets the collection of all currently registered commit templates provided by plugins.
    /// </summary>
    IEnumerable<CommitTemplateItem> RegisteredTemplates { get; }

    /// <summary>
    ///     Loads commit template from the file specified in .git/config
    ///     under <c>commit.template</c> setting.
    /// </summary>
    /// <exception cref="FileNotFoundException">The specified template file cannot be found.</exception>
    /// <returns>The commit template, if it is specified; otherwise <see langword="null" />.</returns>
    /// <remarks>
    ///     Template file can be set by the following command:
    ///     <c>$ git config --global commit.template ~/.git_commit_msg.txt</c>
    /// </remarks>
    string? LoadGitCommitTemplate();

    /// <summary>
    ///     Allows a plugin to register a new commit template.
    /// </summary>
    /// <param name="templateName">The name of the template.</param>
    /// <param name="templateText">The body of the template.</param>
    void Register(string templateName, Func<string> templateText, Image? icon, bool isRegex);

    /// <summary>
    ///     Allows a plugin to unregister a commit template.
    /// </summary>
    /// <param name="templateName">The name of the template.</param>
    void Unregister(string templateName);
}

public sealed class CommitTemplateManager(
    Func<IGitModule> getModule,
    IFullPathResolver fullPathResolver,
    IFileSystem fileSystem)
    : ICommitTemplateManager
{
    private static readonly Lock _registeredTemplatesStorageSync = new();
    private static readonly List<RegisteredCommitTemplateItem> _registeredTemplatesStorage = [];

    public CommitTemplateManager(Func<IGitModule> getModule)
        : this(getModule, new FullPathResolver(() => getModule().WorkingDir), new FileSystem())
    {
    }

    /// <summary>
    ///     Gets the collection of all currently registered commit templates provided by plugins.
    /// </summary>
    IEnumerable<CommitTemplateItem> ICommitTemplateManager.RegisteredTemplates
    {
        get
        {
            lock (_registeredTemplatesStorageSync)
            {
                return _registeredTemplatesStorage
                    .Select(item => new CommitTemplateItem(item.Name, item.Text(), item.Icon, item.IsRegex))
                    .AsReadOnlyList();
            }
        }
    }

    /// <summary>
    ///     Loads commit template from the file specified in .git/config
    ///     under <c>commit.template</c> setting.
    /// </summary>
    /// <exception cref="FileNotFoundException">The specified template file cannot be found.</exception>
    /// <returns>The commit template, if it is specified; otherwise <see langword="null" />.</returns>
    /// <remarks>
    ///     Template file can be set by the following command:
    ///     <c>$ git config --global commit.template ~/.git_commit_msg.txt</c>
    /// </remarks>
    string? ICommitTemplateManager.LoadGitCommitTemplate()
    {
        var fileName = GetModule().GetEffectiveSetting("commit.template");
        if (string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        fileName = fullPathResolver.Resolve(fileName);
        if (!fileSystem.File.Exists(fileName))
        {
            throw new FileNotFoundException("File not found", fileName);
        }

        var commitTemplate = fileSystem.File.ReadAllText(fileName);
        return commitTemplate;
    }

    /// <summary>
    ///     Allows a plugin to register a new commit template.
    /// </summary>
    /// <param name="templateName">The name of the template.</param>
    /// <param name="templateText">The body of the template.</param>
    void ICommitTemplateManager.Register(string templateName, Func<string> templateText, Image? icon,
        bool isRegex = false)
    {
        lock (_registeredTemplatesStorageSync)
        {
            if (_registeredTemplatesStorage.All(item => item.Name != templateName))
            {
                _registeredTemplatesStorage.Add(new RegisteredCommitTemplateItem(templateName, templateText, icon,
                    isRegex));
            }
        }
    }

    /// <summary>
    ///     Allows a plugin to unregister a commit template.
    /// </summary>
    /// <param name="templateName">The name of the template.</param>
    void ICommitTemplateManager.Unregister(string templateName)
    {
        lock (_registeredTemplatesStorageSync)
        {
            _registeredTemplatesStorage.RemoveAll(item => item.Name == templateName);
        }
    }

    private IGitModule GetModule()
    {
        return getModule() ?? throw new ArgumentException($"Require a valid instance of {nameof(IGitModule)}");
    }

    private readonly struct RegisteredCommitTemplateItem(string name, Func<string> text, Image? icon, bool isRegex)
    {
        public readonly string Name = name;

        public readonly Func<string> Text = text;
        public readonly Image? Icon = icon;
        public readonly bool IsRegex = isRegex;
    }
}
