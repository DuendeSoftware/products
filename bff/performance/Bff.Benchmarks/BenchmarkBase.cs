// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using Perfolizer.Horology;
using Perfolizer.Metrology;

namespace Bff.Benchmarks;

public abstract class BenchmarkBase
{

    public static string GetPath(int index) => "/" + RandomWords[index];

    public static readonly string[] RandomWords =
    [
        "planet", "wonderful", "mystery", "journey", "beautiful", "adventure", "discovery", "fantastic", "incredible", "delicious",
        "fabulous", "glorious", "happiness", "inspiration", "magnificent", "miraculous", "opportunity", "passion", "spectacular", "celebration",
        "champion", "creativity", "diamond", "ecstasy", "electric", "enormous", "euphoria", "excellent", "exquisite", "freedom",
        "friendship", "gratitude", "harmony", "heavenly", "imagination", "infinity", "innocence", "laughter", "liberty", "majestic",
        "melody", "miracle", "paradise", "perfect", "phenomenal", "pleasure", "precious", "princess", "radiant", "rapture",
        "serendipity", "sparkle", "sunshine", "sweetheart", "symphony", "tranquility", "treasure", "triumph", "unbelievable", "vacation",
        "vibrant", "victory", "visionary", "wonderful", "yesterday", "zealous", "amazing", "blissful", "breathtaking", "brilliant",
        "captivating", "charming", "dazzling", "delightful", "divine", "dreamy", "effervescent", "elegant", "enchanting", "energetic",
        "engaging", "enjoyable", "enthusiastic", "eternal", "exuberant", "fascinating", "fearless", "flourishing", "gorgeous", "graceful",
        "grateful", "gutsy", "heavenly", "huggable", "idealistic", "illustrious", "immaculate", "impressive", "innovative", "irresistible",
        "joyful", "jubilant", "legendary", "luminous", "luxurious", "magical", "marvelous", "masterful", "meaningful", "mesmerizing",
        "motivated", "optimistic", "outstanding", "passionate", "peaceful", "picturesque", "playful", "powerful", "profound", "proud",
        "rejuvenated", "remarkable", "resilient", "resourceful", "sensational", "serene", "shimmering", "soulful", "splendid", "steadfast",
        "stunning", "sublime", "successful", "superb", "terrific", "thankful", "thriving", "tranquil", "transformative", "trusting",
        "truthful", "unforgettable", "uplifting", "valiant", "versatile", "victorious", "vivacious", "wholesome", "wondrous", "youthful",
        "zesty", "abundance", "accomplish", "achievement", "alignment", "altruistic", "ambitious", "benevolent", "bountiful", "celebrate",
        "charismatic", "compassion", "conscience", "courageous", "courteous", "dedicated", "determined", "diligent", "disciplined", "effulgent",
        "empathetic", "empowered", "endurance", "equanimity", "exceptional", "extraordinary", "exultant", "faithful", "flourish", "forgiveness",
        "fortitude", "generous", "genuine", "gracefully", "gratifying", "harmonious", "heartfelt", "honorable", "humanitarian", "illuminated",
        "imaginative", "inspirational", "integrity", "intuitive", "invigorated", "kindhearted", "knowledgeable", "liberated", "lovable", "magnanimous",
        "mindful", "motivated", "nourishing", "nurturing", "optimism", "passionate", "patient", "persistent", "philanthropic", "pioneering",
        "plentiful", "positive", "proactive", "prosperous", "purposeful", "receptive", "refreshing", "rejoicing", "rejuvenate", "respectful",
        "resplendent", "reverent", "righteous", "serenity", "sincere", "spirited", "spiritual", "stupendous", "thoughtful", "thriving",
        "tolerant", "tranquility", "trustworthy", "unwavering", "upbeat", "virtuous", "vision", "warmhearted", "welcoming", "willing",
        "wonderful", "admirable", "adorable", "adulation", "affection", "affirmation", "ageless", "agreeable", "alluring", "angelic",
        "appealing", "astonishing", "attractive", "authentic", "beautiful", "beloved", "beneficial", "benign", "blessed", "bliss",
        "bubbly", "centered", "charitable", "cherished", "chivalrous", "classic", "clean", "commendable", "considerate", "constant",
        "constructive", "convivial", "courtesy", "cuddly", "darling", "dashing", "dauntless", "dazzle", "decorous", "delicate",
        "desirable", "devoted", "dignified", "distinguished", "dream", "dynamic", "ecstatic", "educate", "effective", "efficacious",
        "electrifying", "eloquent", "embrace", "eminence", "empathy", "encourage", "endearing", "endless", "endorse", "enduring",
        "energize", "enlighten", "enlivened", "enrich", "entertain", "enthralled", "enticing", "entrancing", "equitable", "ethical",
        "eulogize", "exalt", "exceed", "excellence", "exciting", "exemplary", "exhilarating", "exonerate", "exult", "facilitate",
        "fair", "faithful", "fame", "familiar", "famous", "fancy", "fantasia", "fashionable", "faultless", "favorable",
        "favorite", "fearlessness", "feasible", "feat", "felicity", "fertile", "fervent", "festive", "fetching", "fiery",
        "fine", "finesse", "fitting", "flair", "flamboyant", "flawless", "flexible", "flourishing", "fluent", "focused",
        "fond", "forbearing", "forceful", "foremost", "foresight", "forgiving", "formal", "formidable", "forthright", "fortuitous",
        "forward", "foundational", "frank", "freedom", "friendly", "frisky", "frugal", "fruitful", "fulfill", "fun",
        "fundamental", "gallant", "game", "generosity", "genial", "genteel", "gentle", "gentleman", "genuine", "gifted",
        "giving", "glamorous", "gleaming", "gleeful", "glittering", "glowing", "godly", "golden", "good", "goodly",
        "gorgeous", "governing", "grace", "gracious", "grand", "grateful", "gratify", "great", "gregarious", "grounded",
        "growing", "guiding", "handsome", "handy", "happily", "hardy", "harmless", "harmony", "head", "healing",
        "healthy", "heartening", "heaven", "helpful", "heroic", "hilarious", "holy", "honest", "honesty", "honey",
        "honorable", "honored", "hopeful", "hospitable", "hot", "humane", "humble", "humorous", "hygienic", "ideal",
        "idolized", "idyllic", "illuminate", "illustrious", "imaginative", "immaculate", "immense", "impartial", "impassioned", "impeccable",
        "important", "impress", "improve", "improving", "inalienable", "incisive", "included", "incontestable", "incorruptible", "incredible",
        "indebted", "indefatigable", "independent", "indestructible", "individual", "indomitable", "industrious", "ineffable", "inexpensive", "infallible",
        "infinite", "influential", "informative", "ingenious", "ingenuity", "inhabitant", "inherent", "initial", "initiative", "innate",
        "mystery",
        "majestic",
        "momentum",
        "memorable",
        "magnificent",
        "meaningful",
        "marvelous",
        "meditate",
        "melodious",
        "mesmerize",
        "meticulous",
        "midnight",
        "miracle",
        "mischief",
        "modulate",
        "monument",
        "moonlight",
        "motivate",
        "mountain",
        "movement",
        "multiple",
        "multitude",
        "mundane",
        "murmur",
        "musical",
        "mustang",
        "mutable",
        "muttering",
        "mystical",
        "mythical"
    ];

    public abstract Task InitializeAsync();
    public abstract Task DisposeAsync();
}

public class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        var exporter = new CsvExporter(
            CsvSeparator.CurrentCulture,
            new SummaryStyle(
                cultureInfo: System.Globalization.CultureInfo.InvariantCulture,
                printUnitsInHeader: false,
                printUnitsInContent: false,
                timeUnit: TimeUnit.Microsecond,
                sizeUnit: SizeUnit.KB
            ));

        AddJob(Job.LongRun);
        AddColumnProvider(DefaultColumnProviders.Instance);
        AddExporter(exporter);
        AddLogger(new ConsoleLogger()); // Add a minimal logger back if you want basic running info

    }
}
