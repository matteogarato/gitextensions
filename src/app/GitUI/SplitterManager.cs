using GitExtensions.Extensibility.Settings;
using GitExtUtils.GitUI;

namespace GitUI;

public sealed class SplitterManager(SettingsSource settings)
{
    private readonly List<SplitterData> _splitters = [];

    public void AddSplitter(SplitContainer splitter, string settingName, int? defaultDistance = null)
    {
        _splitters.Add(new SplitterData(
            splitter,
            settingName,
            DpiUtil.DpiX,
            defaultDistance));
    }

    public void RestoreSplitters()
    {
        foreach (var splitter in _splitters)
        {
            splitter.RestoreFromSettings(settings);
        }
    }

    public void SaveSplitters()
    {
        foreach (var splitter in _splitters)
        {
            splitter.SaveToSettings(settings);
        }
    }

    internal TestAccessor GetTestAccessor()
    {
        return new TestAccessor(this);
    }

    internal sealed class SplitterData(SplitContainer splitter, string settingName, int dpi, int? defaultDistance)
    {
        private int SplitterSize
        {
            get => splitter.Orientation == Orientation.Horizontal ? splitter.Height : splitter.Width;
        }

        internal string SizeSettingsKey
        {
            get => settingName + "_Size";
        }

        internal string DpiSettingsKey
        {
            get => settingName + "_Dpi";
        }

        internal string DistanceSettingsKey
        {
            get => settingName + "_Distance";
        }

        internal string FontSizeSettingsKey
        {
            get => settingName + "_FontSize";
        }

        internal string Panel1CollapsedSettingsKey
        {
            get => settingName + "_Panel1Collapsed";
        }

        internal string Panel2CollapsedSettingsKey
        {
            get => settingName + "_Panel2Collapsed";
        }

        public void RestoreFromSettings(SettingsSource settings)
        {
            splitter.BeginInit();
            splitter.SuspendLayout();

            var prevDpi = settings.GetInt(DpiSettingsKey) ?? DpiUtil.DpiX;
            var prevSizeSetting = settings.GetInt(SizeSettingsKey);
            var prevSize = prevSizeSetting ?? 0;
            var prevDistanceSetting = settings.GetInt(DistanceSettingsKey);
            var prevDistance = prevDistanceSetting ?? 0;

            if (prevSize > 0 && prevDistance > 0)
            {
                var fixedPanel = splitter.FixedPanel;
                var splitterWidth = splitter.SplitterWidth;
                if (SplitterSize == prevSize && dpi == prevDpi)
                {
                    SetSplitterDistance(fixedPanel == FixedPanel.Panel2 ? prevDistance + splitterWidth : prevDistance);
                }
                else
                {
                    switch (fixedPanel)
                    {
                        case FixedPanel.None:
                            // At this point, the property "SplitterSize" has its original value from design time,
                            // i.e. the actual size after opening the window is unknown yet. The calculation below
                            // determines the resulting splitter distance by the ratio of both sides of the splitter.
                            SetSplitterDistance((float)SplitterSize * prevDistance / prevSize);
                            break;
                        case FixedPanel.Panel1:
                            SetSplitterDistance(DpiUtil.Scale(prevDistance, prevDpi));
                            break;
                        case FixedPanel.Panel2:
                            var panel2PrevSize =
                                DpiUtil.Scale(prevSize, prevDpi) - DpiUtil.Scale(prevDistance, prevDpi);
                            const int paddingOffset = 2; // Refer to FormCommit.ctor+WorkaroundPaddingIncreaseBug
                            SetSplitterDistance(SplitterSize - panel2PrevSize - paddingOffset + splitterWidth);
                            break;
                    }
                }
            }
            else if (defaultDistance.HasValue && !prevSizeSetting.HasValue && !prevDistanceSetting.HasValue)
            {
                SetSplitterDistance(defaultDistance.Value);
            }

            // Keep the collapsed states as in the designer if not stored yet
            splitter.Panel1Collapsed = settings.GetBool(Panel1CollapsedSettingsKey, splitter.Panel1Collapsed);
            splitter.Panel2Collapsed = settings.GetBool(Panel2CollapsedSettingsKey, splitter.Panel2Collapsed);

            splitter.ResumeLayout();
            splitter.EndInit();
        }

        public void SaveToSettings(SettingsSource settings)
        {
            settings.SetInt(DpiSettingsKey, dpi);
            settings.SetInt(SizeSettingsKey, SplitterSize);
            settings.SetInt(DistanceSettingsKey, splitter.SplitterDistance);
            settings.SetFloat(FontSizeSettingsKey, splitter.Font.Size);
            settings.SetBool(Panel1CollapsedSettingsKey, splitter.Panel1Collapsed);
            settings.SetBool(Panel2CollapsedSettingsKey, splitter.Panel2Collapsed);
        }

        private void SetSplitterDistance(float distance)
        {
            try
            {
                var intDistance = Convert.ToInt32(distance);

                if (IsValidSplitterDistance(intDistance))
                {
                    splitter.SplitterDistance = intDistance;
                }
                else if (defaultDistance.HasValue && IsValidSplitterDistance(defaultDistance.Value))
                {
                    splitter.SplitterDistance = defaultDistance.Value;
                }
            }
            catch
            {
                // The attempt to set even the default value has failed.
            }

            bool IsValidSplitterDistance(int d)
            {
                return d > splitter.Panel1MinSize &&
                       d < SplitterSize - splitter.Panel2MinSize;
            }
        }
    }

    internal readonly struct TestAccessor(SplitterManager manager)
    {
        public List<SplitterData> Splitters
        {
            get => manager._splitters;
        }
    }
}
