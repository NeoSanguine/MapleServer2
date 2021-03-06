using Maple2Storage.Types;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapleServer2.Packets
{
    public static class PartyPacket
    {
        public static Packet SendInvite(Player sender)
        {
            PacketWriter pWriter = PacketWriter.Of(SendOp.PARTY)
                .WriteByte(0xB) //Invite
                .WriteUnicodeString(sender.Name)
                .WriteShort(3843) //Unk
                .WriteByte() //Unk
                .WriteByte(); //Unk
            return pWriter;
        }

        public static Packet Create(Player leader)
        {
            PacketWriter pWriter = PacketWriter.Of(SendOp.PARTY);

            CreatePartyHeader(leader, pWriter, 1);

            CharacterListPacket.WriteCharacter(leader, pWriter);
            pWriter.WriteLong();
            pWriter.WriteInt();
            pWriter.WriteShort();
            pWriter.WriteByte();
            WriteSkills(pWriter, leader);
            pWriter.WriteLong();
            return pWriter;
        }

        public static Packet CreateExisting(Player leader, List<Player> members)
        {
            PacketWriter pWriter = PacketWriter.Of(SendOp.PARTY);

            CreatePartyHeader(leader, pWriter, (short) members.Count);

            foreach (Player member in members)
            {
                CharacterListPacket.WriteCharacter(member, pWriter);
                pWriter.WriteLong();
                pWriter.WriteInt();
                pWriter.WriteShort();
                pWriter.WriteByte();
                WriteSkills(pWriter, member);
                pWriter.WriteByte(15);
            }
            pWriter.WriteLong();
            return pWriter;
        }

        //Generates the header code for Create
        public static Packet CreatePartyHeader(Player player, PacketWriter pWriter, short members)
        {
            pWriter.WriteByte(0x9) //Creates party with the # of members
                .WriteByte(0)
                .WriteInt(0)
                .WriteLong(player.CharacterId)
                .WriteShort(1); //# of Party members I think, but it's scuffed
            return pWriter;
        }

        public static Packet Join(Player player)
        {
            PacketWriter pWriter = PacketWriter.Of(SendOp.PARTY)
                .WriteByte(0x2); //Add player to party UI

            CharacterListPacket.WriteCharacter(player, pWriter);
            pWriter.WriteInt();
            WriteSkills(pWriter, player);
            pWriter.WriteLong();
            return pWriter;
        }

        public static Packet UpdatePlayer(Player player)
        {
            PacketWriter pWriter = PacketWriter.Of(SendOp.PARTY)
                .WriteByte(0xD) //Pretty sure this is the party update function, call after Join to update their location, afk status, etc.
                .WriteLong(player.CharacterId);

            CharacterListPacket.WriteCharacter(player, pWriter);
            pWriter.WriteInt();
            WriteSkills(pWriter, player);
            pWriter.WriteLong();
            return pWriter;
        }

        public static Packet UpdateHitpoints(Player player)
        {
            PacketWriter pWriter = PacketWriter.Of(SendOp.PARTY)
                .WriteByte(0x13)
                .WriteLong(player.CharacterId)
                .WriteLong(player.AccountId)
                .WriteInt(player.Stats.Hp.Total)
                .WriteInt(player.Stats.CurrentHp.Min)
                .WriteShort();
            return pWriter;
        }

        public static Packet SetLeader(Player player)
        {
            PacketWriter pWriter = PacketWriter.Of(SendOp.PARTY)
                .WriteByte(0x8)
                .WriteLong(player.CharacterId);
            return pWriter;
        }

        public static Packet Disband()
        {
            PacketWriter pWriter = PacketWriter.Of(SendOp.PARTY)
                .WriteByte(0x7);
            return pWriter;
        }

        public static Packet Leave(Player player, byte self)
        {
            PacketWriter pWriter = PacketWriter.Of(SendOp.PARTY)
                .WriteByte(0x3)
                .WriteLong(player.CharacterId)
                .WriteByte(self); //0 = Other leaving, 1 = Self leaving
            return pWriter;
        }

        public static Packet Kick(Player player)
        {
            PacketWriter pWriter = PacketWriter.Of(SendOp.PARTY)
                .WriteByte(0x4)
                .WriteLong(player.CharacterId);
            return pWriter;
        }

        public static Packet StartReadyCheck(Player leader, List<Player> members, int count)
        {
            PacketWriter pWriter = PacketWriter.Of(SendOp.PARTY)
                .WriteByte(0x2F)
                .WriteByte(2) //unk
                .WriteInt(count)
                .WriteLong(DateTimeOffset.Now.ToUnixTimeSeconds() + Environment.TickCount)
                .WriteInt(members.Count);
            foreach (Player partyMember in members)
            {
                pWriter.WriteLong(partyMember.CharacterId);
            }
            pWriter.WriteInt(1) //unk
                .WriteLong(leader.CharacterId)
                .WriteInt(0); //unk
            return pWriter;

        }
        public static Packet ReadyCheck(Player player, byte accept)
        {
            PacketWriter pWriter = PacketWriter.Of(SendOp.PARTY)
                .WriteByte(0x30)
                .WriteLong(player.CharacterId)
                .WriteByte(accept);
            return pWriter;
        }
        public static Packet EndReadyCheck()
        {
            PacketWriter pWriter = PacketWriter.Of(SendOp.PARTY)
                .WriteByte(0x31);
            return pWriter;
        }

        //Had to copy this method because of the last short being written.
        public static Packet WriteSkills(PacketWriter pWriter, Player character)
        {
            // Get skills
            Dictionary<int, Skill> skills = character.SkillTabs[0].Skills; // Get first skill tab skills only for now, uncertain of how to have multiple skill tabs

            // Ordered list of skill ids (must be sent in this order)
            int[] ids = character.SkillTabs[0].Order;
            byte split = character.SkillTabs[0].Split;
            int countId = ids[ids.Length - split]; // Split to last skill id

            pWriter.WriteByte((byte)(ids.Length - split)); // Skill count minus split

            // List of skills for given tab in format (byte zero) (byte learned) (int skill_id) (int skill_level) (byte zero)
            foreach (int id in ids)
            {
                if (id == countId)
                {
                    pWriter.WriteByte(split); // Write that there are (split) skills left
                }
                pWriter.WriteByte();
                pWriter.WriteByte(skills[id].Learned);
                pWriter.WriteInt(id);
                pWriter.WriteInt((int)skills[id].Level);
                pWriter.WriteByte();
            }

            return pWriter;
        }

    }
}
