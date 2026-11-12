namespace GitUITests;

public sealed class LinqExtensionsTests
{
    [Fact]
    public void AsReadOnlyList_returns_singleton_empty()
    {
        new HashSet<int>().AsReadOnlyList().Should().BeSameAs(Array.Empty<int>());
        new Dictionary<int, int>().Values.AsReadOnlyList().Should().BeSameAs(Array.Empty<int>());
    }

    [Fact]
    public void AsReadOnlyList_copies_to_new_list_if_required()
    {
        HashSet<int> set = [1, 2, 3];

        set.AsReadOnlyList().Should().Equal(1, 2, 3);
    }

    [Fact]
    public void AsReadOnlyList_returns_object_unchanged_when_has_required_interface()
    {
        Test([]);
        Test(new int[1]);
        Test(new List<int>());
        Test(new List<int> { 1, 2, 3 });

        static void Test(IEnumerable<int> e)
        {
            if (e is IReadOnlyList<int>)
            {
                e.AsReadOnlyList().Should().BeSameAs(e);
            }
            else
            {
                e.AsReadOnlyList().Should().NotBeSameAs(e);
            }
        }
    }

    [Fact]
    public void IndexOf()
    {
        int[] ints = [0, 1, 2, 3];

        ints.IndexOf(i => i == 0).Should().Be(0);
        ints.IndexOf(i => i == 1).Should().Be(1);
        ints.IndexOf(i => i == 2).Should().Be(2);
        ints.IndexOf(i => i == 3).Should().Be(3);
        ints.IndexOf(i => i == 4).Should().Be(-1);
    }
}
