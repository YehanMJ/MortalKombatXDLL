using System.Collections.Generic;

namespace MortalKombatXDLL.Models
{
    internal class Room
    {
        public string Name { get; }
        public HashSet<string> Players { get; }

        public Room(string name)
        {
            Name = name;
            Players = new HashSet<string>();
        }
    }
}