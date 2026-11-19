using System;

namespace Yard.Core
{
    // The script's data, mirroring the JSON in the brief. Field names match the JSON keys
    // exactly so JsonUtility can map them. A cue is one flat type: each cue type reads only
    // the fields it needs, and the rest stay at their defaults.

    [Serializable]
    public class YardScript
    {
        public string ticket;
        public MarkerEntry[] opening;
        public Cue[] cues;
        public MarkerEntry[] closing;
    }

    [Serializable]
    public class MarkerEntry
    {
        public string id;
        public string lane;
        public int load;
    }

    [Serializable]
    public class Cue
    {
        public string say;
        public string who;
        public string what;
        public string flag;
        public int minute;

        /// <summary>A label shown during the tick flash. Never added to or subtracted from a load.</summary>
        public int amount;

        public Patch patch;
    }

    [Serializable]
    public class Patch
    {
        public LoadSet[] set;
        public string[] gone;
    }

    [Serializable]
    public class LoadSet
    {
        public string id;
        public int load;
    }
}
