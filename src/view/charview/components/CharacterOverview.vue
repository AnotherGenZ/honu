﻿<template>
    <div>
        <div class="d-flex">
            <table class="table table-sm w-auto d-inline-block mr-5">
                <tr class="table-secondary th-border-top-0">
                    <td colspan="2"><b>General</b></td>
                </tr>

                <tr>
                    <td>Outfit</td>
                    <td>
                        <span v-if="character.outfitID == null">
                            &lt;no outfit&gt;
                        </span>
                        <a v-else :href="'/o/' + character.outfitID">
                            [{{character.outfitTag}}] {{character.outfitName}}
                        </a>
                    </td>
                </tr>

                <tr>
                    <td>Faction</td>
                    <td>{{character.factionID | faction}}</td>
                </tr>

                <tr>
                    <td>Server</td>
                    <td>
                        <a :href="'/view/' + character.worldID" title="View realtime stats">
                            {{character.worldID | world}}
                        </a>
                    </td>
                </tr>

                <tr>
                    <td>Battle rank</td>
                    <td>
                        <span title="Battle rank ~ ASP">
                            {{character.battleRank}}~{{character.prestige}}
                        </span>
                    </td>
                </tr>

                <tr>
                    <td>Created</td>
                    <td>
                        {{character.dateCreated | moment}}
                        ({{character.dateCreated | timeAgo}})
                    </td>
                </tr>

                <tr>
                    <td>Last login</td>
                    <td>
                        <span v-if="character.dateLastLogin.getTime() > 0">
                            {{character.dateLastLogin | moment}}
                            ({{character.dateLastLogin | timeAgo}})
                        </span>
                        <span v-else>
                            &lt;never&gt;
                        </span>
                    </td>
                </tr>

                <tr>
                    <td>
                        Last API update
                        <info-hover text="When this character was last updated in the API. Nothing after this is reflected"></info-hover>
                    </td>
                    <td>
                        <span v-if="character.dateLastSave.getTime() > 0">
                            {{character.dateLastSave | moment}}
                            ({{character.dateLastSave | timeAgo}})
                        </span>
                        <span v-else>
                            &lt;never&gt;
                        </span>
                    </td>
                </tr>

                <tr>
                    <td>
                        Honu update
                        <info-hover text="When Honu last performed a character update"></info-hover>
                    </td>
                    <td>
                        {{character.lastUpdated | moment}}
                        ({{character.lastUpdated | timeAgo}})
                    </td>
                </tr>
            </table>

            <table class="table table-sm w-auto d-inline-block mr-4" v-if="history.state == 'loaded'">
                <thead>
                    <tr class="table-secondary th-border-top-0">
                        <td colspan="2"><b>Lifetime stats</b></td>
                    </tr>
                </thead>

                <tr v-if="history.state == 'idle'"></tr>
                <tr v-else-if="history.state == 'loading'">
                    <td colspan="2">
                        <busy style="max-width: 2rem;"></busy>
                    </td>
                </tr>

                <tbody v-else-if="history.state == 'loaded' && history.data.length > 0">
                    <tr>
                        <td>Kills</td>
                        <td>{{historyKills.allTime | locale}}</td>
                    </tr>

                    <tr>
                        <td>Deaths</td>
                        <td>{{historyDeaths.allTime | locale}}</td>
                    </tr>

                    <tr>
                        <td>Play time</td>
                        <td>{{historyTime.allTime | mduration}}</td>
                    </tr>

                    <tr>
                        <td>KPM</td>
                        <td>{{historyKills.allTime / (historyTime.allTime || 1) * 60 | fixed | locale(2)}}</td>
                    </tr>

                    <tr>
                        <td>K/D</td>
                        <td>{{historyKills.allTime / (historyDeaths.allTime || 1) | fixed | locale(2)}}</td>
                    </tr>

                    <tr>
                        <td>Score</td>
                        <td>{{historyScore.allTime | locale}}</td>
                    </tr>

                    <tr>
                        <td>SPM</td>
                        <td>{{historyScore.allTime / (historyTime.allTime || 1) * 60 | fixed | locale(2)}}</td>
                    </tr>

                    <tr>
                        <td>Certs</td>
                        <td>{{historyCert.allTime | locale(0)}}</td>
                    </tr>

                    <tr>
                        <td>Certs/min</td>
                        <td>{{historyCert.allTime / (historyTime.allTime || 1) * 60 | locale(2)}}</td>
                    </tr>
                </tbody>

                <tr v-else-if="history.state == 'loaded' && history.data.length == 0" class="table-warning">
                    <td colspan="2">
                        Missing history data
                    </td>
                </tr>

            </table>

            <table class="table table-sm w-auto d-inline-block mr-4">
                <thead>
                    <tr class="table-secondary th-border-top-0">
                        <td colspan="2"><b>30 day stats</b></td>
                    </tr>
                </thead>

                <tr v-if="history.state == 'idle'"></tr>
                <tr v-else-if="history.state == 'loading'">
                    <td colspan="2">
                        Loading...
                        <busy style="max-width: 2rem;"></busy>
                    </td>
                </tr>

                <tr v-else-if="history.state == 'nocontent'">
                    <td colspan="2">
                        No history stats
                    </td>
                </tr>
                
                <tbody v-else-if="history.state == 'loaded' && history.data.length > 0">
                    <tr>
                        <td>Kills</td>
                        <td>{{recentKills | locale}}</td>
                    </tr>

                    <tr>
                        <td>Deaths</td>
                        <td>{{recentDeaths | locale}}</td>
                    </tr>

                    <tr>
                        <td>Play time</td>
                        <td>{{recentTime | mduration}}</td>
                    </tr>

                    <tr>
                        <td>KPM</td>
                        <td>{{recentKills / (recentTime || 1) * 60 | fixed | locale(2)}}</td>
                    </tr>

                    <tr>
                        <td>K/D</td>
                        <td>{{recentKills / (recentDeaths || 1) | fixed | locale(2)}}</td>
                    </tr>

                    <tr>
                        <td>Score</td>
                        <td>{{recentScore | locale}}</td>
                    </tr>

                    <tr>
                        <td>SPM</td>
                        <td>{{recentScore / (recentTime || 1) * 60 | fixed | locale(2)}}</td>
                    </tr>

                    <tr>
                        <td>Certs</td>
                        <td>{{recentCert | locale(0)}}</td>
                    </tr>

                    <tr>
                        <td>Certs/min</td>
                        <td>{{recentCert / (recentTime || 1) * 60 | locale(2)}}</td>
                    </tr>
                </tbody>

                <tr v-else-if="history.state == 'loaded' && history.data.length == 0" class="table-warning">
                    <td colspan="2">
                        Historical stats do not exist
                    </td>
                </tr>

                <tr v-else-if="history.state == 'error'">
                    <td colspan="2">
                        <api-error :error="history.problem"></api-error>
                    </td>
                </tr>
            </table>

            <div v-if="stats.state == 'loading'">
                <busy class="honu-busy"></busy>
                Loading...
            </div>

            <character-class-stats v-if="stats.state == 'loaded'" class="mr-4"
                :data="stats.data" type="forever" title="All time" :include-metadata="true">
            </character-class-stats>

            <character-class-stats v-if="stats.state == 'loaded'" class="mr-4"
                :data="stats.data" type="monthly" title="This month" :include-metadata="false">
            </character-class-stats>

            <api-error v-if="stats.state == 'error'" :error="stats.problem"></api-error>
        </div>

        <collapsible header-text="History stats">
            <character-history-stats v-if="history.state == 'loaded' && history.data.length > 0" :stats="history.data"></character-history-stats>
            <div v-else-if="history.state == 'loaded' && history.data.length == 0">
                Historical stats do not exist
            </div>
            <div v-else-if="history.state == 'loading'">
                <busy style="max-width: 5rem;"></busy>
            </div>
            <div v-else-if="history.state == 'error'">
                <api-error :error="history.problem"></api-error>
            </div>
        </collapsible>

        <hr class="border" />

        <h4>Census links</h4>

        <table class="table table-sm w-auto">
            <tbody>
                <tr>
                    <td><b>Character ID</b></td>
                    <td>{{character.id}}</td>
                    <td>
                        <a :href="'https://census.daybreakgames.com/s:example/get/ps2:v2/character?character_id=' + character.id" target="_blank">
                            Census
                            <span class="ph-bold ph-arrow-square-out"></span>
                        </a>
                    </td>
                </tr>

                <tr v-if="character.outfitID != null">
                    <td><b>Outfit ID</b></td>
                    <td>{{character.outfitID}}</td>
                    <td>
                        <a :href="'https://census.daybreakgames.com/s:example/get/ps2:v2/outfit?outfit_id=' + character.outfitID" target="_blank">
                            Census
                            <span class="ph-bold ph-arrow-square-out"></span>
                        </a>
                    </td>
                </tr>
            </tbody>
        </table>

    </div>
</template>

<script lang="ts">
    import Vue, { PropType } from "vue";
    import { Loading, Loadable } from "Loading";

    import InfoHover from "components/InfoHover.vue";
    import Busy from "components/Busy.vue";
    import Collapsible from "components/Collapsible.vue";
    import ApiError from "components/ApiError";

    import { PsCharacter } from "api/CharacterApi";
    import { CharacterHistoryStat, CharacterHistoryStatApi } from "api/CharacterHistoryStatApi";
    import { CharacterStat, CharacterStatApi } from "api/CharacterStatApi";
    import { CharacterMetadata, CharacterMetadataApi } from "api/CharacterMetadataApi";

    import CharacterClassStats from "./CharacterClassStats.vue";
    import CharacterHistoryStats from "./CharacterHistoryStats.vue";

    import "filters/LocaleFilter";
    import "filters/FixedFilter";
    import "filters/FactionNameFilter";
    import "filters/WorldNameFilter";
    import "filters/TimeAgoFilter";
    import "MomentFilter";

    export const CharacterOverview = Vue.extend({
        props: {
            character: { type: Object as PropType<PsCharacter>, required: true }
        },

        data: function() {
            return {
                history: Loadable.idle() as Loading<CharacterHistoryStat[]>,
                stats: Loadable.idle() as Loading<CharacterStat[]>,
            }
        },

        mounted: function(): void {
            this.$nextTick(() => {
                this.loadHistory();
                this.loadStats();
            });
        },

        methods: {
            loadHistory: async function(): Promise<void> {
                this.history = Loadable.loading();
                this.history = await CharacterHistoryStatApi.getByCharacterID(this.character.id);
            },

            loadStats: async function(): Promise<void> {
                this.stats = Loadable.loading();
                this.stats = await CharacterStatApi.getByCharacterID(this.character.id);
            }
        },

        computed: {
            recentKills: function(): number {
                return this.historyKills == null ? -1 : this.historyKills.days.reduce((a, b) => a + b, 0);
            },

            recentDeaths: function(): number {
                return this.historyDeaths == null ? -1 : this.historyDeaths.days.reduce((a, b) => a + b, 0);
            },

            recentTime: function(): number {
                return this.historyTime == null ? -1 : this.historyTime.days.reduce((a, b) => a + b, 0);
            },

            recentScore: function(): number {
                return this.historyScore == null ? -1 : this.historyScore.days.reduce((a, b) => a + b, 0);
            },

            recentCert: function(): number {
                return this.historyCert == null ? -1 : this.historyCert.days.reduce((a, b) => a + b, 0);
            },

            currentMonth: function(): number {
                return new Date().getMonth(); // 0 indexed
            },

            historyKills: function(): CharacterHistoryStat | null { return this.history.state != "loaded" ? null : this.history.data.find(iter => iter.type == "kills") || null; },
            historyDeaths: function(): CharacterHistoryStat | null { return this.history.state != "loaded" ? null : this.history.data.find(iter => iter.type == "deaths") || null; },
            historyScore: function(): CharacterHistoryStat | null { return this.history.state != "loaded" ? null : this.history.data.find(iter => iter.type == "score") || null; },
            historyTime: function(): CharacterHistoryStat | null { return this.history.state != "loaded" ? null : this.history.data.find(iter => iter.type == "time") || null; },
            historyCert: function(): CharacterHistoryStat | null { return this.history.state != "loaded" ? null : this.history.data.find(iter => iter.type == "certs") || null; },
        },

        components: {
            CharacterClassStats,
            CharacterHistoryStats,
            Busy, InfoHover, Collapsible, ApiError
        }

    });
    export default CharacterOverview;
</script>
