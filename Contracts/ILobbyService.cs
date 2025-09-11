using System;
using System.Collections.Generic;
using System.ServiceModel;
using MortalKombatXDLL.Models;

namespace MortalKombatXDLL.Contracts
{
    [ServiceContract(CallbackContract = typeof(ILobbyCallback))]
    public interface ILobbyService
    {
        // 1. User Management
        [OperationContract]
        bool Login(string username);

        [OperationContract(IsOneWay = true)]
        void Logout();

        // 2. Lobby Room Management
        [OperationContract]
        bool CreateRoom(string roomName);

        [OperationContract]
        bool JoinRoom(string roomName);

        [OperationContract(IsOneWay = true)]
        void LeaveRoom();

        [OperationContract]
        List<string> GetRooms();

        [OperationContract]
        List<string> GetPlayersInRoom(string roomName);

        // 3. Message Distribution
        [OperationContract(IsOneWay = true)]
        void SendRoomMessage(string message);

        // 4. Private Messaging
        [OperationContract(IsOneWay = true)]
        void SendPrivateMessage(string targetUsername, string message);

        // 5. File Sharing (images, text)
        [OperationContract(IsOneWay = true)]
        void SendFile(string fileName, string mimeType, byte[] data, FileType fileType, string optionalCaption);

        // (Optional) Keep-alive
        [OperationContract(IsOneWay = true)]
        void Heartbeat();
    }
}