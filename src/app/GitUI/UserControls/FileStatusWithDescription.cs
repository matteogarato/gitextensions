using GitExtensions.Extensibility.Git;
using GitUI.Properties;

namespace GitUI.UserControls;

public class FileStatusWithDescription(
    GitRevision? firstRev,
    GitRevision secondRev,
    string summary,
    IReadOnlyList<GitItemStatus> statuses,
    ObjectId baseA = default,
    ObjectId baseB = default,
    string iconName = nameof(Images.Diff))
{
    public GitRevision? FirstRev { get; } = firstRev;
    public GitRevision SecondRev { get; } = secondRev ?? throw new ArgumentNullException(nameof(secondRev));
    public string Summary { get; } = summary ?? throw new ArgumentNullException(nameof(summary));

    public IReadOnlyList<GitItemStatus> Statuses { get; } =
        statuses ?? throw new ArgumentNullException(nameof(statuses));

    public ObjectId BaseA { get; } = baseA;
    public ObjectId BaseB { get; } = baseB;
    public string IconName { get; } = iconName ?? throw new ArgumentNullException(nameof(iconName));
}
