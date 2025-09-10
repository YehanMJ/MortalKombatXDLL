using System;
using System.ServiceModel;
using MortalKombatXDLL.Models;

namespace MortalKombatXDLL.Contracts
{
    // Client-side callback contract (duplex).
    public interface ILobbyCallback
    {
        [OperationContract(IsOneWay = true)]
        void OnSystemNotice(string message);

        [OperationContract(IsOneWay = true)]
        void OnUserJoined(string roomName, string username, DateTime utc);

        [OperationContract(IsOneWay = true)]
        void OnUserLeft(string roomName, string username, DateTime utc);

        [OperationContract(IsOneWay = true)]
        void OnRoomMessage(string roomName, string fromUser, string message, DateTime utc);

        [OperationContract(IsOneWay = true)]
        void OnPrivateMessage(string fromUser, string message, DateTime utc);

        [OperationContract(IsOneWay = true)]
        void OnFileReceived(FileDescriptor file, byte[] data);
    }
}