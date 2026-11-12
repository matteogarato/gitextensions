using System.Net;
using System.Text;
using GitUI.Editor;
using ResourceManager;

namespace GitUITests.Editor;

public class RichTextBoxXhtmlSupportExtensionTests : IDisposable
{
    private const string _defaultLinkText = "link";
    private const string _defaultLinkUri = "https://uri.org";
    private const string _defaultPrefix = "pre ";
    private const string _defaultSuffix = " suf";
    private const string _linkSeparator = "|||";
    private readonly ILinkFactory _linkFactory = null!;

    private readonly RichTextBox _rtb = null!;

    public RichTextBoxXhtmlSupportExtensionTests()
    {
        _rtb = new RichTextBox();
        _linkFactory = new LinkFactory();
    }

    void IDisposable.Dispose()
    {
        _rtb.Dispose();
    }

    private void SetupLink(string prefix, string linkText, string uri, string suffix)
    {
        StringBuilder text = new();

        if (prefix is not null)
        {
            text.Append(prefix);
        }

        if (uri is not null)
        {
            if (linkText is null)
            {
                text.Append(WebUtility.HtmlEncode(uri));
            }
            else
            {
                text.Append(_linkFactory.CreateLink(linkText, uri));
            }
        }

        if (suffix is not null)
        {
            text.Append(suffix);
        }

        _rtb.SetXhtmlText(text.ToString());
    }

    private void SetupDefaultLink()
    {
        SetupLink(_defaultPrefix, _defaultLinkText, _defaultLinkUri, _defaultSuffix);
    }

    [Fact]
    public void GetLink_should_return_null_if_index_is_invalid()
    {
        SetupLink("", null!, null!, "");
        _rtb.GetLink(-1).Should().BeNull();
        _rtb.GetLink(0).Should().BeNull();

        SetupDefaultLink();
        _rtb.GetLink(-1).Should().BeNull();
        _rtb.GetLink(_rtb.Text.Length).Should().BeNull();
    }

    [Fact]
    public void GetLink_should_return_null_if_left_of_link()
    {
        SetupDefaultLink();
        for (var index = 0; index < _defaultPrefix.Length; ++index)
        {
            _rtb.GetLink(index).Should().BeNull();
        }
    }

    [Fact]
    public void GetLink_should_return_null_if_right_of_link()
    {
        SetupDefaultLink();
        for (var index = _defaultPrefix.Length + _defaultLinkText.Length + _defaultLinkUri.Length +
                         _linkSeparator.Length;
             index < _rtb.Text.Length;
             ++index)
        {
            _rtb.GetLink(index).Should().BeNull();
        }
    }

    [Fact]
    public void GetLink_should_return_uri_if_text_contains_hash()
    {
        SetupLink(string.Empty, "#hash", _defaultLinkUri, string.Empty);
        _rtb.GetLink(0).Should().Be(_defaultLinkUri);
    }

    [Fact]
    public void GetLink_should_return_uri_if_uri_contains_hash()
    {
        var uri = _defaultLinkUri + "#hash";
        SetupLink(string.Empty, _defaultLinkText, uri, string.Empty);
        _rtb.GetLink(0).Should().Be(uri);
    }

    [Fact]
    public void GetLink_should_return_uri_if_text_and_uri_contain_hash()
    {
        var uri = _defaultLinkUri + "#hash";
        SetupLink(string.Empty, "#text", uri, string.Empty);
        _rtb.GetLink(0).Should().Be(uri);
    }

    [Fact]
    public void GetLink_should_return_uri_if_at_link()
    {
        SetupDefaultLink();
        for (var index = _defaultPrefix.Length; index < _defaultPrefix.Length + _defaultLinkText.Length; ++index)
        {
            _rtb.GetLink(index).Should().Be(_defaultLinkUri);
        }
    }

    [Fact]
    public void GetLink_should_return_uri_if_begins_with_link()
    {
        SetupLink(null!, _defaultLinkText, _defaultLinkUri, _defaultSuffix);
        _rtb.GetLink(0).Should().Be(_defaultLinkUri);
    }

    [Fact]
    public void GetLink_should_return_uri_if_link_at_line_begin()
    {
        SetupLink(_defaultPrefix + "\n", _defaultLinkText, _defaultLinkUri, _defaultSuffix);
        _rtb.GetLink(_defaultPrefix.Length + 1).Should().Be(_defaultLinkUri);
    }

    [Fact]
    public void GetLink_should_return_uri_if_ends_with_link()
    {
        SetupLink(_defaultPrefix, _defaultLinkText, _defaultLinkUri, null!);
        _rtb.GetLink(_defaultPrefix.Length).Should().Be(_defaultLinkUri);
    }

    [Fact]
    public void GetLink_should_return_uri_if_link_at_line_end()
    {
        SetupLink(_defaultPrefix, _defaultLinkText, _defaultLinkUri, "\n" + _defaultSuffix);
        _rtb.GetLink(_defaultPrefix.Length).Should().Be(_defaultLinkUri);
    }

    [Fact]
    public void GetLink_should_return_uri_if_comma_after_link()
    {
        SetupLink(_defaultPrefix, _defaultLinkText, _defaultLinkUri, "," + _defaultSuffix);
        _rtb.GetLink(_defaultPrefix.Length).Should().Be(_defaultLinkUri);
    }

    [Fact]
    public void GetLink_should_return_uri_if_without_link_text()
    {
        SetupLink(_defaultPrefix, null!, _defaultLinkUri, _defaultSuffix);
        _rtb.GetLink(_defaultPrefix.Length).Should().Be(null);
    }

    [Fact]
    public void GetLink_should_return_uri_if_ends_with_link_without_link_text()
    {
        SetupLink(_defaultPrefix, null!, _defaultLinkUri, null!);
        _rtb.GetLink(_defaultPrefix.Length).Should().Be(null);
    }
}
