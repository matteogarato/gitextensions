using GitExtUtils.GitUI;

namespace GitUI.UserControls;

partial class FileStatusList
{
    private void LoadFileIcons(IEnumerable<TreeNode> nodes, CancellationToken cancellationToken)
    {
        var missingIconsByExtension = FindMissingIcons(nodes);
        if (missingIconsByExtension.Count > 0 && Module?.WorkingDir is { } workingDir)
        {
            ThreadHelper.FileAndForget(() => LoadFileIconsAsync(missingIconsByExtension,
                fileName => _iconProvider.Get(workingDir, fileName), _imageListData.ImageList.ImageSize,
                FileStatusListView, cancellationToken));
        }

        return;

        static async Task LoadFileIconsAsync(Dictionary<string, MissingIcon> missingIconsByExtension,
            Func<string, Icon?> loadFileIcon, Size size, Control control, CancellationToken cancellationToken)
        {
            List<Image> icons = new(missingIconsByExtension.Count);
            foreach (var missingIcon in missingIconsByExtension)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                missingIcon.Value.RelativeImageIndex = icons.Count;
                icons.Add(loadFileIcon(missingIcon.Value.ExampleFileName) is { } icon
                    ? Scale(icon.ToBitmap(), _imageListData.IconSize, size, offsetY: 0)
                    : _imageListData.DefaultFileImage);
            }

            await control.SwitchToMainThreadAsync(cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var imageIndexOffset = _imageListData.ImageList.Images.Count;
            _imageListData.ImageList.Images.AddRange([.. icons]);
            foreach (var missingIcon in missingIconsByExtension)
            {
                _imageListData.StateImageIndexMap[missingIcon.Key] =
                    missingIcon.Value.RelativeImageIndex + imageIndexOffset;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var delayCounter = 0;
            foreach (var missingIcon in missingIconsByExtension)
            {
                var imageIndex = missingIcon.Value.RelativeImageIndex + imageIndexOffset;
                foreach (var node in missingIcon.Value.Nodes)
                {
                    node.ImageIndex = imageIndex;
                    node.SelectedImageIndex = imageIndex;

                    if (++delayCounter >= 100)
                    {
                        delayCounter = 0;
                        await Task.Delay(1, cancellationToken);
                    }
                }
            }

            control.Invalidate();
        }

        static Dictionary<string, MissingIcon> FindMissingIcons(IEnumerable<TreeNode> nodes)
        {
            Dictionary<string, MissingIcon> missingIconsByExtension = [];

            var defaultFileImageIndex = _imageListData.StateImageIndexMap[nameof(ImageListData.DefaultFileImage)];
            foreach (var node in nodes)
            {
                if (node.ImageIndex != defaultFileImageIndex)
                {
                    continue;
                }

                var fileNode = node.Tag is GroupKey
                    ? node.Items().First(node => node.Nodes.Count == 0)
                    : node;
                if (fileNode.Tag is not FileStatusItem fileStatusItem)
                {
                    continue;
                }

                var fileName = fileStatusItem.Item.Name;
                var extension = Path.GetExtension(fileName);
                if (_imageListData.StateImageIndexMap.TryGetValue(extension, out var imageIndex))
                {
                    node.ImageIndex = imageIndex;
                    node.SelectedImageIndex = imageIndex;
                }
                else if (missingIconsByExtension.TryGetValue(extension, out var entry))
                {
                    entry.Nodes.Add(node);
                }
                else
                {
                    missingIconsByExtension[extension] = new MissingIcon(fileName, [node]);
                }
            }

            return missingIconsByExtension;
        }
    }
}
