using UNChat.Context;
using UNChat.Models;

public static class DbInitializer
{
    public static void Initialize(UNChatDbContext context)
    {
        context.Database.EnsureCreated();

        if (context.Emojis.Any()) return;

        var emojis = new Emoji[]
        {
            new Emoji { Symbol = "😀" },
            new Emoji { Symbol = "😂" },
            new Emoji { Symbol = "😍" },
            new Emoji { Symbol = "😎" },
            new Emoji { Symbol = "😢" },
            new Emoji { Symbol = "🤔" },
            new Emoji { Symbol = "🎉" },
            new Emoji { Symbol = "❤️" },
            new Emoji { Symbol = "👍" },
            new Emoji { Symbol = "👏" }
        };

        context.Emojis.AddRange(emojis);
        context.SaveChanges();
    }
}
