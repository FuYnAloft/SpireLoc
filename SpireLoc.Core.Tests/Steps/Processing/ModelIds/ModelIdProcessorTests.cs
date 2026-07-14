using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Models;
using SpireLoc.Core.Steps.Processing.ModelIds;
using Xunit;

namespace SpireLoc.Core.Tests.Steps.Processing.ModelIds;

public sealed class ModelIdProcessorTests
{
    [Fact]
    public void VanillaProcessorTransformsModelTablesAndKeepsOtherTablesUntouched()
    {
        var bundle = Bundle(
            ("cards", [Entry(["CustomCard", "title"])]),
            ("orbs", [Entry(["CustomOrb", "description"])]),
            ("static_hover_tips", [Entry(["CustomHoverTip", "title"])]),
            ("settings_ui", [Entry(["CustomSetting", "title"])]));

        var result = new VanillaModelIdProcessor(ModelIdDirection.ToGame).Process(bundle);

        Assert.Equal("CUSTOM_CARD", result[Path("cards")][0].Key[0]);
        Assert.Equal("CUSTOM_ORB", result[Path("orbs")][0].Key[0]);
        Assert.Equal("CUSTOM_HOVER_TIP", result[Path("static_hover_tips")][0].Key[0]);
        Assert.Equal("CustomSetting", result[Path("settings_ui")][0].Key[0]);
    }

    [Fact]
    public void BaseLibProcessorRoundTripsAncientAndSeaGlassCharacterIds()
    {
        var source = Bundle(
            ("ancients", [Entry(["CustomAncient", "talk", "CustomCharacter", "0-0", "ancient"])]),
            ("relics", [
                Entry(["CustomRelic", "title"]),
                Entry(["SEA_GLASS", "CustomCharacter", "title"]),
            ]),
            ("static_hover_tips", [Entry(["CustomHoverTip", "title"])]));
        var toGame = new BaseLibModelIdProcessor(ModelIdDirection.ToGame, "myMod");
        var toSource = new BaseLibModelIdProcessor(ModelIdDirection.ToSource, "myMod");

        var game = toGame.Process(source);
        var reverted = toSource.Process(game);

        Assert.Equal("MYMOD-CUSTOM_ANCIENT", game[Path("ancients")][0].Key[0]);
        Assert.Equal("MYMOD-CUSTOM_CHARACTER", game[Path("ancients")][0].Key[2]);
        Assert.Equal("MYMOD-CUSTOM_RELIC", game[Path("relics")][0].Key[0]);
        Assert.Equal("SEA_GLASS", game[Path("relics")][1].Key[0]);
        Assert.Equal("MYMOD-CUSTOM_CHARACTER", game[Path("relics")][1].Key[1]);
        Assert.Equal("MYMOD-CUSTOM_HOVER_TIP", game[Path("static_hover_tips")][0].Key[0]);
        Assert.Equal(source, reverted);
    }

    [Fact]
    public void RitsuLibProcessorUsesEachTableCategoryAndHandlesBothAfflictionTableSpellings()
    {
        var source = Bundle(
            ("achievements", [Entry(["CustomAchievement", "title"])]),
            ("acts", [Entry(["CustomAct", "title"])]),
            ("afflications", [Entry(["CustomAffliction", "title"])]),
            ("afflictions", [Entry(["OtherAffliction", "title"])]),
            ("card_keywords", [Entry(["CustomKeyword", "title"])]),
            ("cards", [Entry(["CustomCard", "title"])]),
            ("characters", [Entry(["CustomCharacter", "title"])]),
            ("enchantments", [Entry(["CustomEnchantment", "title"])]),
            ("encounters", [Entry(["CustomEncounter", "title"])]),
            ("events", [Entry(["CustomEvent", "title"])]),
            ("model_capabilities", [Entry(["CustomCapability", "title"])]),
            ("modelcapabilities", [Entry(["LegacyCapability", "title"])]),
            ("modifiers", [Entry(["CustomModifier", "title"])]),
            ("monsters", [Entry(["CustomMonster", "title"])]),
            ("orbs", [Entry(["CustomOrb", "title"])]),
            ("potions", [Entry(["CustomPotion", "title"])]),
            ("powers", [Entry(["CustomPower", "title"])]),
            ("relics", [Entry(["CustomRelic", "title"])]));

        var result = new RitsuLibModelIdProcessor(ModelIdDirection.ToGame, "my-mod").Process(source);

        Assert.Equal("MY_MOD_ACHIEVEMENT_CUSTOM_ACHIEVEMENT", Key(result, "achievements"));
        Assert.Equal("MY_MOD_ACT_CUSTOM_ACT", Key(result, "acts"));
        Assert.Equal("MY_MOD_AFFLICTION_CUSTOM_AFFLICTION", Key(result, "afflications"));
        Assert.Equal("MY_MOD_AFFLICTION_OTHER_AFFLICTION", Key(result, "afflictions"));
        Assert.Equal("MY_MOD_KEYWORD_CUSTOM_KEYWORD", Key(result, "card_keywords"));
        Assert.Equal("MY_MOD_CARD_CUSTOM_CARD", Key(result, "cards"));
        Assert.Equal("MY_MOD_CHARACTER_CUSTOM_CHARACTER", Key(result, "characters"));
        Assert.Equal("MY_MOD_ENCHANTMENT_CUSTOM_ENCHANTMENT", Key(result, "enchantments"));
        Assert.Equal("MY_MOD_ENCOUNTER_CUSTOM_ENCOUNTER", Key(result, "encounters"));
        Assert.Equal("MY_MOD_EVENT_CUSTOM_EVENT", Key(result, "events"));
        Assert.Equal("MY_MOD_MODEL_CAPABILITY_CUSTOM_CAPABILITY", Key(result, "model_capabilities"));
        Assert.Equal("MY_MOD_MODELCAPABILITY_LEGACY_CAPABILITY", Key(result, "modelcapabilities"));
        Assert.Equal("MY_MOD_MODIFIER_CUSTOM_MODIFIER", Key(result, "modifiers"));
        Assert.Equal("MY_MOD_MONSTER_CUSTOM_MONSTER", Key(result, "monsters"));
        Assert.Equal("MY_MOD_ORB_CUSTOM_ORB", Key(result, "orbs"));
        Assert.Equal("MY_MOD_POTION_CUSTOM_POTION", Key(result, "potions"));
        Assert.Equal("MY_MOD_POWER_CUSTOM_POWER", Key(result, "powers"));
        Assert.Equal("MY_MOD_RELIC_CUSTOM_RELIC", Key(result, "relics"));
    }

    [Fact]
    public void RitsuLibProcessorRoundTripsAncientAndSeaGlassCharacterIdsButSkipsStaticHoverTips()
    {
        var source = Bundle(
            ("ancients", [Entry(["CustomAncient", "talk", "CustomCharacter", "0-0", "ancient"])]),
            ("relics", [Entry(["SEA_GLASS", "CustomCharacter", "title"])]),
            ("static_hover_tips", [Entry(["CustomHoverTip", "title"])]));
        var toGame = new RitsuLibModelIdProcessor(ModelIdDirection.ToGame, "myMod");
        var toSource = new RitsuLibModelIdProcessor(ModelIdDirection.ToSource, "myMod");

        var game = toGame.Process(source);
        var reverted = toSource.Process(game);

        Assert.Equal("MY_MOD_ANCIENT_CUSTOM_ANCIENT", game[Path("ancients")][0].Key[0]);
        Assert.Equal("MY_MOD_CHARACTER_CUSTOM_CHARACTER", game[Path("ancients")][0].Key[2]);
        Assert.Equal("MY_MOD_CHARACTER_CUSTOM_CHARACTER", game[Path("relics")][0].Key[1]);
        Assert.Equal("CustomHoverTip", game[Path("static_hover_tips")][0].Key[0]);
        Assert.Equal(source, reverted);
    }

    [Fact]
    public void ProcessorRejectsUnknownDirection()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new VanillaModelIdProcessor((ModelIdDirection)42));
    }

    [Fact]
    public void ProcessorForwardsTransformDiagnosticsToTheProvidedCollection()
    {
        var diagnostics = new DiagnosticCollection();
        var bundle = Bundle(("cards", [Entry(["unexpected", "title"])]));

        new BaseLibModelIdProcessor(ModelIdDirection.ToSource, "myMod").Process(bundle, diagnostics);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "ModelIdTransform.UnexpectedGameId");
        Assert.Contains(diagnostics,
            diagnostic => diagnostic.Message.StartsWith("[zhs/cards#0]", StringComparison.Ordinal));
    }

    private static string Key(LocBundle bundle, string tableName) => bundle[Path(tableName)][0].Key[0];

    private static LocBundle Bundle(params (string TableName, LocEntry[] Entries)[] tables) =>
        new(tables.Select(table => KeyValuePair.Create(Path(table.TableName), new LocTable(table.Entries))));

    private static LocEntry Entry(string[] key) => new(key, "value");

    private static LocTablePath Path(string tableName) => new("zhs", tableName);
}
