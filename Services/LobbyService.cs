using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using MortalKombatXDLL.Contracts;
using MortalKombatXDLL.Models;

namespace MortalKombatXDLL.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single,
                     ConcurrencyMode = ConcurrencyMode.Multiple,
                     IncludeExceptionDetailInFaults = true)]
    public class LobbyService : ILobbyService
    {
        private readonly ConcurrentDictionary<string, PlayerSession> _players =
            new ConcurrentDictionary<string, PlayerSession>(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<string, Room> _rooms =
            new ConcurrentDictionary<string, Room>(StringComparer.OrdinalIgnoreCase);

        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        public LobbyService()
        {
            // Optionally create a default room
            _rooms.TryAdd("Main", new Room("Main"));
        }

        public bool Login(string username)
        {
            ValidateNonEmpty(username, "username");

            var callback = OperationContext.Current.GetCallbackChannel<ILobbyCallback>();
            var channel = OperationContext.Current.Channel;

            if (_players.ContainsKey(username))
                return false;

            var session = new PlayerSession
            {
                Username = username,
                Callback = callback,
                CurrentRoom = null,
                ConnectedUtc = DateTime.UtcNow,
                Channel = channel
            };

            if (!_players.TryAdd(username, session))
                return false;

            channel.Closed += (s, e) => ForceLogout(username);
            channel.Faulted += (s, e) => ForceLogout(username);

            SafeSystemNotice(username + " logged in.");
            return true;
        }

        public void Logout()
        {
            var username = GetCurrentUser();
            if (username == null) return;
            ForceLogout(username);
        }

        public bool CreateRoom(string roomName)
        {
            ValidateNonEmpty(roomName, "roomName");
            return _rooms.TryAdd(roomName, new Room(roomName));
        }

        public bool JoinRoom(string roomName)
        {
            ValidateNonEmpty(roomName, "roomName");

            var username = RequireCurrentUser();
            if (!_rooms.TryGetValue(roomName, out var room))
                return false;

            var session = _players[username];

            string previousRoom = session.CurrentRoom;

            if (previousRoom != null &&
                _rooms.TryGetValue(previousRoom, out var oldRoom))
            {
                if (oldRoom.Players.Remove(username))
                    BroadcastUserLeft(oldRoom.Name, username);
            }

            lock (room.Players)
            {
                room.Players.Add(username);
            }

            session.CurrentRoom = room.Name;
            BroadcastUserJoined(room.Name, username);

            return true;
        }

        public void LeaveRoom()
        {
            var username = RequireCurrentUser();
            var session = _players[username];
            if (session.CurrentRoom == null) return;

            if (_rooms.TryGetValue(session.CurrentRoom, out var room))
            {
                lock (room.Players)
                {
                    if (room.Players.Remove(username))
                        BroadcastUserLeft(room.Name, username);
                }
            }

            session.CurrentRoom = null;
        }

        public List<string> GetRooms()
        {
            return _rooms.Keys.OrderBy(k => k).ToList();
        }

        public List<string> GetPlayersInRoom(string roomName)
        {
            if (_rooms.TryGetValue(roomName, out var room))
            {
                lock (room.Players)
                {
                    return room.Players.OrderBy(p => p).ToList();
                }
            }
            return new List<string>();
        }

        public void SendRoomMessage(string message)
        {
            var username = RequireCurrentUser();
            var session = _players[username];
            if (session.CurrentRoom == null) return;

            DateTime utc = DateTime.UtcNow;
            foreach (var target in GetRoomMembers(session.CurrentRoom))
            {
                SafeInvoke(target, cb => cb.OnRoomMessage(session.CurrentRoom, username, message, utc));
            }
        }

        public void SendPrivateMessage(string targetUsername, string message)
        {
            var from = RequireCurrentUser();
            if (!_players.TryGetValue(targetUsername, out var target))
                return;

            var fromSession = _players[from];
            if (fromSession.CurrentRoom == null ||
                fromSession.CurrentRoom != target.CurrentRoom)
                return; // Must be in same room.

            DateTime utc = DateTime.UtcNow;
            SafeInvoke(targetUsername, cb => cb.OnPrivateMessage(from, message, utc));
            if (!string.Equals(from, targetUsername, StringComparison.OrdinalIgnoreCase))
                SafeInvoke(from, cb => cb.OnPrivateMessage(from, message, utc));
        }

        public void SendFile(string fileName, string mimeType, byte[] data, FileType fileType, string optionalCaption)
        {
            var username = RequireCurrentUser();
            var session = _players[username];
            if (session.CurrentRoom == null) return;
            if (data == null || data.Length == 0) return;

            if (fileType == FileType.Image && !IsImageMime(mimeType)) return;
            if (fileType == FileType.Text && !IsTextMime(mimeType)) return;

            var fd = new FileDescriptor
            {
                FileName = fileName,
                MimeType = mimeType,
                SizeBytes = data.LongLength,
                Sender = username,
                Room = session.CurrentRoom,
                FileType = fileType,
                Caption = optionalCaption,
                UtcSent = DateTime.UtcNow
            };

            foreach (var member in GetRoomMembers(session.CurrentRoom))
            {
                SafeInvoke(member, cb => cb.OnFileReceived(fd, data));
            }
        }

        public void Heartbeat()
        {
            // Could be used for timeout logic later.
        }

        // Helper methods

        private IEnumerable<string> GetRoomMembers(string roomName)
        {
            if (_rooms.TryGetValue(roomName, out var room))
            {
                lock (room.Players)
                {
                    return room.Players.ToList();
                }
            }
            return Enumerable.Empty<string>();
        }

        private string GetCurrentUser()
        {
            var callback = OperationContext.Current.GetCallbackChannel<ILobbyCallback>();
            // O(n) scan acceptable for small user counts.
            foreach (var kvp in _players)
            {
                if (kvp.Value.Callback == callback)
                    return kvp.Key;
            }
            return null;
        }

        private string RequireCurrentUser()
        {
            var user = GetCurrentUser();
            if (user == null)
                throw new FaultException("Not logged in.");
            return user;
        }

        private void ForceLogout(string username)
        {
            if (!_players.TryRemove(username, out var session))
                return;

            if (session.CurrentRoom != null &&
                _rooms.TryGetValue(session.CurrentRoom, out var room))
            {
                lock (room.Players)
                {
                    if (room.Players.Remove(username))
                        BroadcastUserLeft(room.Name, username);
                }
            }

            SafeSystemNotice(username + " disconnected.");
        }

        private void BroadcastUserJoined(string roomName, string username)
        {
            DateTime utc = DateTime.UtcNow;
            foreach (var member in GetRoomMembers(roomName))
            {
                SafeInvoke(member, cb => cb.OnUserJoined(roomName, username, utc));
            }
        }

        private void BroadcastUserLeft(string roomName, string username)
        {
            DateTime utc = DateTime.UtcNow;
            foreach (var member in GetRoomMembers(roomName))
            {
                if (!string.Equals(member, username, StringComparison.OrdinalIgnoreCase))
                {
                    SafeInvoke(member, cb => cb.OnUserLeft(roomName, username, utc));
                }
            }
        }

        private void SafeSystemNotice(string msg)
        {
            foreach (var kvp in _players.Keys.ToList())
            {
                SafeInvoke(kvp, cb => cb.OnSystemNotice(msg));
            }
        }

        private void SafeInvoke(string username, Action<ILobbyCallback> action)
        {
            if (!_players.TryGetValue(username, out var session))
                return;

            try
            {
                action(session.Callback);
            }
            catch
            {
                // Assume client faulted; remove.
                ForceLogout(username);
            }
        }

        private static void ValidateNonEmpty(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new FaultException(name + " cannot be empty.");
        }

        private static bool IsImageMime(string mime)
        {
            if (string.IsNullOrEmpty(mime)) return false;
            mime = mime.ToLowerInvariant();
            return mime.StartsWith("image/");
        }

        private static bool IsTextMime(string mime)
        {
            if (string.IsNullOrEmpty(mime)) return false;
            mime = mime.ToLowerInvariant();
            return mime == "text/plain" || mime.StartsWith("text/");
        }
    }
}