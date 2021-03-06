﻿using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Enums;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Tools;
using MapleServer2.Types;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;

namespace MapleServer2.PacketHandlers.Game
{
    public class PartyHandler : GamePacketHandler
    {
        public override RecvOp OpCode => RecvOp.PARTY;

        public PartyHandler(ILogger<PartyHandler> logger) : base(logger) { }

        public override void Handle(GameSession session, PacketReader packet)
        {
            byte mode = packet.ReadByte(); //Mode

            switch (mode)
            {
                //Party invite
                case 0x1:
                    HandleInvite(session, packet);
                    break;
                //Party join
                case 0x2:
                    HandleJoin(session, packet);
                    break;
                //Party leave
                case 0x3:
                    HandleLeave(session, packet);
                    break;
                //Kick player
                case 0x4:
                    HandleKick(session, packet);
                    break;
                //Set party leader
                case 0x11:
                    HandleSetLeader(session, packet);
                    break;
                //Vote kicking
                case 0x2D:
                    HandleVoteKick(session, packet);
                    break;
                //Ready check start
                case 0x2E:
                    HandleStartReadyCheck(session, packet);
                    break;
                case 0x30:
                    HandleReadyCheckUpdate(session, packet);
                    break;
            }
        }

        private void HandleInvite(GameSession session, PacketReader packet)
        {
            string target = packet.ReadUnicodeString();

            Player other = GameServer.Storage.GetPlayerByName(target);
            if (other == null)
            {
                return;
            }
            if (other.PartyId == 0)
            {
                other.Session.Send(PartyPacket.SendInvite(session.Player));
                if (session.Player.PartyId == 0)
                {
                    session.Send(PartyPacket.Create(session.Player));
                }
                //pSession.Send(ChatPacket.Send(session.Player, "You were invited to a party by " + session.Player.Name, ChatType.NoticeAlert));
            }
            else
            {
                session.Send(ChatPacket.Send(session.Player, other.Session.Player.Name + " is already in a party.", ChatType.NoticeAlert2));
            }
        }

        private void HandleJoin(GameSession session, PacketReader packet)
        {
            string target = packet.ReadUnicodeString(); //Who invited the player
            int accept = packet.ReadByte(); //If the player accepted
            int unknown = packet.ReadInt(); //Something something I think it's dungeon not sure

            Player partyLeader = GameServer.Storage.GetPlayerByName(target);
            if (partyLeader == null)
            {
                return;
            }
            GameSession leaderSession = partyLeader.Session;
            if (accept == 1)
            {
                Party party = GameServer.PartyManager.GetPartyByLeader(partyLeader);
                if (party != null)
                {
                    //Existing party, add joining player to all other party members.
                    party.BroadcastPacketParty(PartyPacket.Join(session.Player));
                    party.BroadcastPacketParty(PartyPacket.UpdatePlayer(session.Player));
                    party.BroadcastPacketParty(PartyPacket.UpdateHitpoints(session.Player));
                    party.AddMember(session.Player);
                }
                else
                {
                    //Create new party
                    Party newParty = new Party(GuidGenerator.Int(), 10, new List<Player> { partyLeader, session.Player });
                    GameServer.PartyManager.AddParty(newParty);

                    //Send the party leader all the stuff for the joining player
                    leaderSession.Send(PartyPacket.Join(session.Player));
                    leaderSession.Send(PartyPacket.UpdatePlayer(session.Player));
                    leaderSession.Send(PartyPacket.UpdateHitpoints(session.Player));

                    leaderSession.Send(PartyPacket.UpdateHitpoints(partyLeader));

                    partyLeader.PartyId = newParty.Id;

                    party = newParty;
                }

                session.Player.PartyId = party.Id;

                //Create existing party based on the list of party members
                session.Send(PartyPacket.CreateExisting(partyLeader, party.Members));
                session.Send(PartyPacket.UpdatePlayer(session.Player));
                foreach (Player partyMember in party.Members)
                {
                    //Skip first character because of the scuffed Create packet. For now this is a workaround and functions the same.
                    if (partyMember.CharacterId != party.Members.First().CharacterId)
                    {
                        //Adds the party member to the UI
                        session.Send(PartyPacket.Join(partyMember));
                    }
                    //Update the HP for each party member.
                    session.Send(PartyPacket.UpdateHitpoints(partyMember));
                }
                //Sometimes the party leader doesn't get set correctly. Not sure how to fix.
            }
            else
            {
                //Send Decline message to inviting player
                leaderSession.Send(ChatPacket.Send(partyLeader, session.Player.Name + " declined the invitation.", ChatType.NoticeAlert2));
            }
        }

        private void HandleSetLeader(GameSession session, PacketReader packet)
        {
            string target = packet.ReadUnicodeString();

            Player newLeader = GameServer.Storage.GetPlayerByName(target);
            if (newLeader == null)
            {
                return;
            }

            Party party = GameServer.PartyManager.GetPartyByLeader(session.Player);
            if (party == null)
            {
                return;
            }

            party.BroadcastPacketParty(PartyPacket.SetLeader(newLeader));
            party.Leader = newLeader;
            party.Members.Remove(newLeader);
            party.Members.Insert(0, newLeader);
        }

        private void HandleLeave(GameSession session, PacketReader packet)
        {
            Party party = GameServer.PartyManager.GetPartyById(session.Player.PartyId);
            session.Send(PartyPacket.Leave(session.Player, 1)); //1 = You're the player leaving
            session.Player.PartyId = 0;
            if (party == null)
            {
                return;
            }
            party.RemoveMember(session.Player);
            party.BroadcastPacketParty(PartyPacket.Leave(session.Player, 0));
            if (party.Leader.CharacterId == session.Player.CharacterId)
            {
                party.FindNewLeader();
            }
            party.CheckDisband();
        }

        private void HandleKick(GameSession session, PacketReader packet)
        {
            long charId = packet.ReadLong();

            Party party = GameServer.PartyManager.GetPartyByLeader(session.Player);
            if (party == null)
            {
                return;
            }

            Player kickedPlayer = GameServer.Storage.GetPlayerById(charId);
            if (kickedPlayer == null)
            {
                return;
            }

            party.BroadcastPacketParty(PartyPacket.Kick(kickedPlayer));
            party.RemoveMember(kickedPlayer);
            kickedPlayer.PartyId = 0;

            if (party.Leader.CharacterId == kickedPlayer.CharacterId)
            {
                party.FindNewLeader();
            }
            party.CheckDisband();
        }

        private void HandleVoteKick(GameSession session, PacketReader packet)
        {
            long charId = packet.ReadLong();

            Party party = GameServer.PartyManager.GetPartyById(session.Player.PartyId);
            if (party == null)
            {
                return;
            }

            Player kickedPlayer = GameServer.Storage.GetPlayerById(charId);
            if (kickedPlayer == null)
            {
                return;
            }

            party.BroadcastPacketParty(ChatPacket.Send(session.Player, session.Player.Name + " voted to kick " + kickedPlayer.Name, ChatType.NoticeAlert3));
            //TODO: Keep a counter of vote kicks for a player?
        }

        private void HandleStartReadyCheck(GameSession session, PacketReader packet)
        {
            Party party = GameServer.PartyManager.GetPartyByLeader(session.Player);
            if (party == null)
            {
                return;
            }
            party.BroadcastPacketParty(PartyPacket.StartReadyCheck(session.Player, party.Members, party.ReadyChecks++));
            party.RemainingMembers = party.Members.Count - 1;
        }

        private void HandleReadyCheckUpdate(GameSession session, PacketReader packet)
        {
            int checkNum = packet.ReadInt() + 1; //+ 1 is because the ReadyChecks variable is always 1 ahead
            byte accept = packet.ReadByte();

            Party party = GameServer.PartyManager.GetPartyById(session.Player.PartyId);
            if (party == null)
            {
                return;
            }
            if (checkNum != party.ReadyChecks)
            {
                return;
            }
            party.BroadcastPacketParty(PartyPacket.ReadyCheck(session.Player, accept));
            party.RemainingMembers -= 1;
            if (party.RemainingMembers == 0)
            {
                party.BroadcastPacketParty(PartyPacket.EndReadyCheck());
            }
        }
    }
}
