using System;
using System.ServiceModel;
using MortalKombatXDLL.Contracts;

namespace MortalKombatXDLL.Models
{
    internal class PlayerSession
    {
        public string Username { get; set; }
        public ILobbyCallback Callback { get; set; }
        public string CurrentRoom { get; set; }
        public DateTime ConnectedUtc { get; set; }
        public IContextChannel Channel { get; set; }
    }
}