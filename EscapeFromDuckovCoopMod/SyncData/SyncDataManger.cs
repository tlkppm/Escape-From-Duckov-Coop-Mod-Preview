// Escape-From-Duckov-Coop-Mod-Preview
// Copyright (C) 2025  Mr.sans and InitLoader's team
//
// This program is not a free software.
// It's distributed under a license based on AGPL-3.0,
// with strict additional restrictions:
//  YOU MUST NOT use this software for commercial purposes.
//  YOU MUST NOT use this software to run a headless game server.
//  YOU MUST include a conspicuous notice of attribution to
//  Mr-sans-and-InitLoader-s-team/Escape-From-Duckov-Coop-Mod-Preview as the original author.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.

namespace EscapeFromDuckovCoopMod;

public class EquipmentSyncData
{
    public string ItemId;
    public int SlotHash;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(SlotHash);
        writer.Put(ItemId ?? "");
    }

    public static EquipmentSyncData Deserialize(NetPacketReader reader)
    {
        return new EquipmentSyncData
        {
            SlotHash = reader.GetInt(),
            ItemId = reader.GetString()
        };
    }
}

public class WeaponSyncData
{
    public string ItemId;
    public int SlotHash;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(SlotHash);
        writer.Put(ItemId ?? "");
    }

    public static WeaponSyncData Deserialize(NetPacketReader reader)
    {
        return new WeaponSyncData
        {
            SlotHash = reader.GetInt(),
            ItemId = reader.GetString()
        };
    }
}

public static class SyncDataManger
{
}