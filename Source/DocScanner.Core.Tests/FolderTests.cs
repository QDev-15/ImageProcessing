namespace DocScanner.Core.Tests;

public class FolderTests
{
    [Fact]
    public void Documents_are_filed_into_folders_and_keep_their_order()
    {
        using var root = new TempRoot();
        var store = new DocumentStore(root.Path);
        DocumentRecord a = store.Create("A"), b = store.Create("B"), c = store.Create("C");
        FolderRecord f = store.CreateFolder("Hợp đồng");

        Assert.Equal(2, store.MoveToFolder([c.Id, a.Id], f.Id));
        // Same order as before (newest first), whatever order they were moved in.
        Assert.Equal(["C", "A"], store.List().Where(d => d.FolderId == f.Id).Select(d => d.Name));
        Assert.Equal(["B"], store.List().Where(d => d.FolderId == null).Select(d => d.Name));

        // Survives a restart.
        var again = new DocumentStore(root.Path);
        Assert.Equal("Hợp đồng", again.Folders().Single().Name);
        Assert.Equal(f.Id, again.Get(c.Id)!.FolderId);

        Assert.Equal(1, again.MoveToFolder([c.Id], null));
        Assert.Null(again.Get(c.Id)!.FolderId);
        Assert.Equal(0, again.MoveToFolder([a.Id], "no-such-folder"));
    }

    [Fact]
    public void Deleting_a_folder_keeps_its_documents()
    {
        using var root = new TempRoot();
        var store = new DocumentStore(root.Path);
        DocumentRecord a = store.Create("A");
        FolderRecord f = store.CreateFolder("X");
        store.MoveToFolder([a.Id], f.Id);
        Assert.True(store.RenameFolder(f.Id, "Y"));
        Assert.Equal("Y", store.Folder(f.Id)!.Name);

        Assert.True(store.DeleteFolder(f.Id));
        Assert.Empty(store.Folders());
        Assert.Null(store.Get(a.Id)!.FolderId);
        Assert.Single(store.List());
    }

    [Fact]
    public void Folders_are_listed_by_name()
    {
        using var root = new TempRoot();
        var store = new DocumentStore(root.Path);
        store.CreateFolder("b");
        store.CreateFolder("A");
        store.CreateFolder("c");
        Assert.Equal(["A", "b", "c"], store.Folders().Select(f => f.Name));
    }

    [Theory]
    [InlineData("Hợp đồng thuê nhà", "hop dong", true)]
    [InlineData("Hợp đồng thuê nhà", "NHÀ thuê", true)]
    [InlineData("Đơn xin việc", "don", true)]
    [InlineData("Tài liệu 27-09-2026", "27-09", true)]
    [InlineData("Tài liệu 27-09-2026", "hop", false)]
    [InlineData("anything", "", true)]
    public void Search_ignores_case_and_diacritics(string name, string query, bool expected) =>
        Assert.Equal(expected, TextSearch.Matches(name, query));
}
