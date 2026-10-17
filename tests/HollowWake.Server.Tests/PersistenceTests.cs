using HollowWake.Core.Characters;
using HollowWake.Core.Items;
using HollowWake.Server.Persistence;
using Xunit;

namespace HollowWake.Server.Tests;

/// <summary>The JSON stores: a character's own file, and the account's boards that ride on it while it plays.</summary>
public class PersistenceTests
{
    [Fact]
    public void TheAccountVaultLoadsWithACharacterAndSavesBackToTheAccount()
    {
        var root = Path.Combine(Path.GetTempPath(), "hw-persist-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var stores = new JsonFileStores(root);
            ICharacterStore characters = stores;
            stores.Save(new Account { Id = "acct", Name = "acct" });
            var relic = new ItemInstance { Uid = 41, Base = "relic_shard", Rarity = "magic", X = 1, Y = 0 };
            var sheet = new CharacterSheet { Id = "c1", AccountId = "acct", Name = "Wren", Calling = "holdfast" };
            sheet.Containers.Boards["personal_stash:1"] = new List<ItemInstance> { new() { Uid = 42, Base = "iron_helm", X = 3, Y = 2 } };
            sheet.Vault = new ContainerSave();
            sheet.Vault.Boards["reliquary"] = new List<ItemInstance> { relic };
            sheet.Vault.Bought["account_stash"] = 1;
            characters.Save(sheet);

            // The character's file has its own boards and not the account's.
            var file = File.ReadAllText(Path.Combine(root, "characters", "c1.json"));
            Assert.Contains("personal_stash:1", file);
            Assert.DoesNotContain("reliquary", file);
            Assert.Equal(41, Assert.Single(stores.Load("acct")!.Vault.Boards["reliquary"]).Uid);

            // A sibling loads with the same vault.
            characters.Save(new CharacterSheet { Id = "c2", AccountId = "acct", Name = "Ash", Calling = "holdfast" });
            var sibling = characters.Load("c2")!;
            Assert.Equal(1, sibling.Vault!.Bought["account_stash"]);
            var item = Assert.Single(sibling.Vault.Boards["reliquary"]);
            Assert.Equal((1, 0), (item.X, item.Y));

            // A save from before containers loads with empty boards.
            File.WriteAllText(Path.Combine(root, "characters", "c3.json"), "{\"id\":\"c3\",\"account_id\":\"acct\",\"name\":\"Old\",\"calling\":\"holdfast\"}");
            var old = characters.Load("c3")!;
            Assert.Empty(old.Containers.Boards);
            Assert.NotNull(old.Vault);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
