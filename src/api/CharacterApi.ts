﻿import { Loading, Loadable } from "Loading";
import ApiWrapper from "api/ApiWrapper";

import { KillEvent } from "api/KillStatApi";

export class PsCharacter {
	public id: string = "";
	public name: string = "";
	public worldID: number = 0;

	public outfitID: string | null = null;
	public outfitTag: string | null = null;
	public outfitName: string | null = null;

	public factionID: number = 0;
	public battleRank: number = 0;
	public prestige: number = 0;

	public lastUpdated: Date = new Date();

	public dateCreated: Date = new Date();
	public dateLastLogin: Date = new Date();
	public dateLastSave: Date = new Date();
}

export class HonuCharacterData {
	public id: string = "";
	public outfitID: string | null = null;
	public factionID: number = 0;
	public teamID: number = 0;
	public worldID: number = 0;
	public online: boolean = false;
	public zoneID: number = 0;
	public latestEventTimestamp: number = 0;
	public latestDeath: KillEvent | null = null;
	public sessionID: number | null = null;
	public lastLogin: Date | null = null;
}

export class MinimalCharacter {
	public id: string = "";
	public outfitID: string | null = null;
	public outfitTag: string | null = null;
	public name: string = "";
	public factionID: number = 0;
}

export class OnlinePlayer {
	public characterID: string = "";
	public character: PsCharacter | null = null;
	public player: HonuCharacterData = new HonuCharacterData();
}

export class FlatOnlinePlayer {
	public characterID: string = "";

	public outfitID: string | null = null;
	public factionID: number | null = null;
	public teamID: number = 0;
	public worldID: number = 0;
	public online: boolean = false;
	public zoneID: number = 0;
	public latestEventTimestamp: number = 0;
	public latestDeath: KillEvent | null = null;
	public sessionID: number | null = null;
	public lastLogin: Date | null = null;

	public name: string | null = null;
	public outfitTag: string | null = null;
	public outfitName: string | null = null;
	public display: string = "";

	public battleRank: number | null = null;
	public prestige: number | null = null;
	public battleRankValue: number = 0;
	public sessionDuration: number | null = null;
}

export class CharacterApi extends ApiWrapper<PsCharacter> {
	private static _instance: CharacterApi = new CharacterApi();
	public static get(): CharacterApi { return this._instance; }

	public static parse(elem: any): PsCharacter {
		return {
			...elem,
			lastUpdated: new Date(elem.lastUpdated),
			dateCreated: new Date(elem.dateCreated),
			dateLastLogin: new Date(elem.dateLastLogin),
			dateLastSave: new Date(elem.dateLastSave)
		}
	}

	public static parseMinimal(elem: any): MinimalCharacter {
		return {
			...elem
		};
    }

	public static parseHonuData(elem: any): HonuCharacterData {
		return {
			...elem,
			lastLogin: (elem.lastLogin == null) ? null : new Date(elem.lastLogin),
			latestDeath: null
        }
    }

	public static parseOnlinePlayer(elem: any): OnlinePlayer {
		return {
			characterID: elem.characterID,
			character: (elem.character == null) ? null : CharacterApi.parse(elem.character),
			player: CharacterApi.parseHonuData(elem.player)
		};
	}

	public static parseFlatOnlinePlayer(elem: any): FlatOnlinePlayer {
		const i: OnlinePlayer = CharacterApi.parseOnlinePlayer(elem);

		return {
			characterID: i.characterID,

			outfitID: i.character?.outfitID ?? null,
			factionID: i.character?.factionID ?? null,
			teamID: i.player.teamID,
			worldID: i.player.worldID,
			online: i.player.online,
			zoneID: i.player.zoneID,
			latestEventTimestamp: i.player.latestEventTimestamp,
			latestDeath: i.player.latestDeath,
			sessionID: i.player.sessionID,
			lastLogin: i.player.lastLogin,

			name: i.character?.name ?? null,
			outfitTag: i.character?.outfitTag ?? null,
			outfitName: i.character?.outfitName ?? null,
			display: ((i.character?.outfitID != null) ? `[${i.character?.outfitTag ?? ""}] ` : "") + (i.character?.name ?? ""),

			battleRank: i.character?.battleRank ?? null,
			prestige: i.character?.prestige ?? null,

			battleRankValue: ((i.character?.prestige ?? 0) * 100) + (i.character?.battleRank ?? 0),
			sessionDuration: i.player.lastLogin == null ? null : (new Date().getTime() - (i.player.lastLogin?.getTime()))
		};
	}

	public static async getByID(charID: string): Promise<Loading<PsCharacter>> {
		return CharacterApi.get().readSingle(`/api/character/${charID}`, CharacterApi.parse);
	}

	public static async getByIDs(charIDs: string[]): Promise<Loading<PsCharacter[]>> {
		if (charIDs.length == 0) {
			return Loadable.loaded([]);
        }

		const chars: PsCharacter[] = [];

		for (let i = 0; i < charIDs.length; i += 200) {
			const slice: string[] = charIDs.slice(i, i + 200);

			const l: Loading<PsCharacter[]> = await CharacterApi.getByIDsInternal(slice);
			if (l.state != "loaded") {
				return l;
			} else {
				chars.push(...l.data);
            }
        }

		return Loadable.loaded(chars);
	}

	public static async getByIDsInternal(charIDs: string[]): Promise<Loading<PsCharacter[]>> {
		const params: URLSearchParams = new URLSearchParams();
		for (const charID of charIDs) {
			params.append("IDs", charID);
		}

		return CharacterApi.get().readList(`/api/character/many?${params.toString()}`, CharacterApi.parse);
	}

	public static async getByName(name: string): Promise<Loading<PsCharacter[]>> {
		return CharacterApi.get().readList(`/api/characters/name/${name}`, CharacterApi.parse);
	}

	public static async searchByName(name: string, censusTimeout: boolean = true): Promise<Loading<PsCharacter[]>> {
		return CharacterApi.get().readList(`/api/characters/search/${name}?censusTimeout=${censusTimeout}`, CharacterApi.parse);
	}

	public static async getHonuData(charID: string): Promise<Loading<HonuCharacterData>> {
		return CharacterApi.get().readSingle(`/api/character/${charID}/honu-data`, CharacterApi.parseHonuData);
    }

	public static async getOnline(): Promise<Loading<FlatOnlinePlayer[]>> {
		return CharacterApi.get().readList(`/api/character/online/players`, CharacterApi.parseFlatOnlinePlayer);
	}

}