// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.Storage;

public sealed class StorageInstanceRouterTests
{
    private static readonly StorageInstanceId Named = StorageInstanceId.Create("named");
    private static readonly StorageInstanceId OtherNamed = StorageInstanceId.Create("other-named");
    private static readonly DataCategoryName CategoryA = DataCategoryName.Create("category-a");
    private static readonly DataCategoryName CategoryB = DataCategoryName.Create("category-b");

    [Fact]
    public void GetAll_returns_empty_when_no_instance_registered()
    {
        var storageInstanceRouter = new StorageInstanceRouter();

        storageInstanceRouter.GetAll().ShouldBeEmpty();
    }

    [Fact]
    public void GetAll_does_not_include_Default_unless_it_was_registered()
    {
        var storageInstanceRouter = new StorageInstanceRouter();
        storageInstanceRouter.RegisterInstance(Named);

        storageInstanceRouter.GetAll().ShouldBe([Named]);
    }

    [Fact]
    public void GetAll_includes_Default_when_it_was_registered()
    {
        var storageInstanceRouter = new StorageInstanceRouter();
        storageInstanceRouter.RegisterInstance(StorageInstanceId.Default);
        storageInstanceRouter.RegisterInstance(Named);

        storageInstanceRouter.GetAll().ShouldBe([StorageInstanceId.Default, Named]);
    }

    [Fact]
    public void RegisterInstance_called_multiple_times_for_same_instance_is_not_duplicated()
    {
        var storageInstanceRouter = new StorageInstanceRouter();
        storageInstanceRouter.RegisterInstance(Named);
        storageInstanceRouter.RegisterInstance(Named);
        storageInstanceRouter.RegisterInstance(Named);

        storageInstanceRouter.GetAll().ShouldBe([Named]);
    }

    [Fact]
    public void Multiple_categories_mapped_to_the_same_instance_only_yields_that_instance_once_from_GetAll()
    {
        var storageInstanceRouter = new StorageInstanceRouter();
        storageInstanceRouter.RegisterInstance(Named);
        storageInstanceRouter.AddMapping(CategoryA, Named);
        storageInstanceRouter.AddMapping(CategoryB, Named);

        storageInstanceRouter.GetAll().ShouldBe([Named]);
        storageInstanceRouter.Resolve(CategoryA).ShouldBe(Named);
        storageInstanceRouter.Resolve(CategoryB).ShouldBe(Named);
    }

    [Fact]
    public void Resolve_falls_back_to_Default_when_category_has_no_mapping_even_if_Default_was_never_registered()
    {
        var storageInstanceRouter = new StorageInstanceRouter();
        storageInstanceRouter.RegisterInstance(Named);

        storageInstanceRouter.Resolve(CategoryA).ShouldBe(StorageInstanceId.Default);
    }

    [Fact]
    public void Resolve_returns_mapped_instance_when_category_is_mapped()
    {
        var storageInstanceRouter = new StorageInstanceRouter();
        storageInstanceRouter.RegisterInstance(Named);
        storageInstanceRouter.AddMapping(CategoryA, Named);

        storageInstanceRouter.Resolve(CategoryA).ShouldBe(Named);
    }

    [Fact]
    public void GetAll_returns_each_registered_instance_exactly_once_when_multiple_named_instances_are_registered()
    {
        var storageInstanceRouter = new StorageInstanceRouter();
        storageInstanceRouter.RegisterInstance(Named);
        storageInstanceRouter.RegisterInstance(OtherNamed);

        storageInstanceRouter.GetAll().Count.ShouldBe(2);
        storageInstanceRouter.GetAll().ShouldContain(Named);
        storageInstanceRouter.GetAll().ShouldContain(OtherNamed);
    }

    [Fact]
    public void AddMapping_throws_when_category_already_mapped_to_a_different_instance()
    {
        var storageInstanceRouter = new StorageInstanceRouter();
        storageInstanceRouter.AddMapping(CategoryA, Named);

        _ = Should.Throw<InvalidOperationException>(() => storageInstanceRouter.AddMapping(CategoryA, OtherNamed));
    }
}
