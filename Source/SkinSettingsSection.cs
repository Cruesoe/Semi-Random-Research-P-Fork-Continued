using System;
using Verse;

namespace CM_Semi_Random_Research
{
    // One settings section: its body draws into a listing, so the plain page and a themed page share it.
    public sealed class SkinSettingsSection
    {
        public readonly string key;
        public readonly string title;
        public readonly Action<Listing_Standard> body;

        public SkinSettingsSection(string key, string title, Action<Listing_Standard> body)
        {
            this.key = key;
            this.title = title;
            this.body = body;
        }
    }
}
