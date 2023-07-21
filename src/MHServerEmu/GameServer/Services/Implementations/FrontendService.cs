﻿using Gazillion;
using Google.ProtocolBuffers;
using MHServerEmu.Common;
using MHServerEmu.Networking;

namespace MHServerEmu.GameServer.Services.Implementations
{
    public class FrontendService : GameService
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        private bool _simulateQueue = false;

        public FrontendService(GameServerManager gameServerManager) : base(gameServerManager)
        {
        }

        public override void Handle(FrontendClient client, ushort muxId, GameMessage[] messages)
        {
            foreach (GameMessage message in messages)
            {
                switch ((FrontendProtocolMessage)message.Id)
                {
                    case FrontendProtocolMessage.ClientCredentials:
                        Logger.Info($"Received ClientCredentials message on muxId {muxId}:");
                        ClientCredentials clientCredentials = ClientCredentials.ParseFrom(message.Content);
                        Logger.Trace(clientCredentials.ToString());
                        Cryptography.SetIV(clientCredentials.Iv.ToByteArray());
                        byte[] decryptedToken = Cryptography.DecryptSessionToken(clientCredentials);
                        Logger.Trace($"Decrypted token: {decryptedToken.ToHexString()}");

                        // Generate response
                        if (_simulateQueue)
                        {
                            Logger.Info("Responding with LoginQueueStatus message");

                            byte[] response = LoginQueueStatus.CreateBuilder()
                                .SetPlaceInLine(1337)
                                .SetNumberOfPlayersInLine(9001)
                                .Build().ToByteArray();

                            client.SendGameMessage(muxId, (byte)FrontendProtocolMessage.LoginQueueStatus, response);
                        }
                        else
                        {
                            Logger.Info("Responding with SessionEncryptionChanged message");

                            byte[] response = SessionEncryptionChanged.CreateBuilder()
                                .SetRandomNumberIndex(1)
                                .SetEncryptedRandomNumber(ByteString.Empty)
                                .Build().ToByteArray();

                            //client.SendGameMessage(muxId, (byte)FrontendProtocolMessage.SessionEncryptionChanged, response);
                            // SessionEncryptionChanged seems to deviate from the standard payload structure, so we'll send a canned response here for now
                            client.SendPacketFromFile("SessionEncryptionChanged.bin");
                        }

                        break;

                    case FrontendProtocolMessage.InitialClientHandshake:
                        Logger.Info($"Received InitialClientHandshake message on muxId {muxId}:");
                        InitialClientHandshake initialClientHandshake = InitialClientHandshake.ParseFrom(message.Content);
                        Logger.Trace(initialClientHandshake.ToString());

                        if (initialClientHandshake.ServerType == PubSubServerTypes.PLAYERMGR_SERVER_FRONTEND)
                        {
                            client.FinishedPlayerMgrServerFrontendHandshake = true;
                        }
                        else if (initialClientHandshake.ServerType == PubSubServerTypes.GROUPING_MANAGER_FRONTEND)
                        {
                            client.FinishedGroupingManagerFrontendHandshake = true;

                            byte[] queueLoadingScreenMessage = NetMessageQueueLoadingScreen.CreateBuilder()
                                .SetRegionPrototypeId(0)
                                .Build().ToByteArray();

                            client.SendGameMessage(muxId, (byte)GameServerToClientMessage.NetMessageQueueLoadingScreen, queueLoadingScreenMessage);

                            client.SendPacketFromFile("NetMessageAchievementDatabaseDump.bin");
                            client.SendPacketFromFile("NetMessageEntityEnterGameWorld.bin");

                            var chatBroadcastMessage = ChatBroadcastMessage.CreateBuilder()
                                .SetRoomType(ChatRoomTypes.CHAT_ROOM_TYPE_BROADCAST_ALL_SERVERS)
                                //.SetFromPlayerName("System")
                                //.SetTheMessage(ChatMessage.CreateBuilder().SetBody("Operation Omega is now active. Will you fight to defend S.H.I.E.L.D.?  Or will you support the evil HYDRA?"))
                                .SetFromPlayerName("MHServerEmu")
                                .SetTheMessage(ChatMessage.CreateBuilder().SetBody("Hello world 2023"))
                                .SetPrestigeLevel(6)
                                .Build().ToByteArray();

                            client.SendGameMessage(2, (byte)GroupingManagerMessage.ChatBroadcastMessage, chatBroadcastMessage);

                            // NetMessageMarkFirstGameFrame.bin
                            var markFirstGameFrameMessage = NetMessageMarkFirstGameFrame.CreateBuilder()
                                .SetCurrentservergametime(161351681600)
                                .SetCurrentservergameid(1125336957151523818)
                                .SetGamestarttime(1)
                                .Build().ToByteArray();

                            var serverVersionMessage = NetMessageServerVersion.CreateBuilder().SetVersion("1.52.0.1700").Build().ToByteArray();

                            /*
                            var liveTuningMessage = NetMessageLiveTuningUpdate.CreateBuilder().SetTuningTypeKeyValueSettings(NetStructLiveTuningSettingProtoEnumValue.CreateBuilder()

                                )
                                .Build.ToByteArray();
                            */

                            // Send hardcoded data after the initial handshakes finish
                            client.SendPacketFromFile("NetMessageMarkFirstGameFrame.bin");
                            client.SendPacketFromFile("NetMessageModifyCommunityMember.bin");
                            client.SendPacketFromFile("NetMessageQueryIsRegionAvailable.bin");
                            client.SendPacketFromFile("NetMessageMarkFirstGameFrame2.bin");
                        }

                        break;

                    default:
                        Logger.Warn($"Received unhandled message {(FrontendProtocolMessage)message.Id} (id {message.Id})");
                        break;
                }
            }
        }
    }
}
