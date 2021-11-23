﻿<template>
    <table class="table table-sm w-auto d-inline-block">
        <tr class="table-secondary">
            <td><b>{{title}}</b></td>
            <td><b>Seconds as</b></td>
            <td><b>Score</b></td>
            <td><b>SPM</b></td>
            <td v-if="IncludeMetadata == true">
                <b>Last updated</b>
                <info-hover text="When this data was last updated in the PS2 API">
                </info-hover>
            </td>
        </tr>

        <tr>
            <td><b>Infil</b></td>
            <td>
                {{timeAs.infil | mduration}}
            </td>
            <td>
                {{score.infil | locale}}
            </td>
            <td>
                {{score.infil / (timeAs.infil || 1) * 60 | fixed | locale}}
            </td>
            <td v-if="IncludeMetadata == true">
                {{infilUpdated | moment}}
            </td>
        </tr>

        <tr>
            <td><b>Light Assault</b></td>
            <td>
                {{timeAs.lightAssault | mduration}}
            </td>
            <td>
                {{score.lightAssault | locale}}
            </td>
            <td>
                {{score.lightAssault / (timeAs.lightAssault || 1) * 60 | fixed | locale}}
            </td>
            <td v-if="IncludeMetadata == true">
                {{lightAssaultUpdated | moment}}
            </td>
        </tr>

        <tr>
            <td><b>Medic</b></td>
            <td>
                {{timeAs.medic | mduration}}
            </td>
            <td>
                {{score.medic | locale}}
            </td>
            <td>
                {{score.medic / (timeAs.medic || 1) * 60 | fixed | locale}}
            </td>
            <td v-if="IncludeMetadata == true">
                {{medicUpdated | moment}}
            </td>
        </tr>

        <tr>
            <td><b>Engineer</b></td>
            <td>
                {{timeAs.engineer | mduration}}
            </td>
            <td>
                {{score.engineer | locale}}
            </td>
            <td>
                {{score.engineer / (timeAs.engineer || 1) * 60 | fixed | locale}}
            </td>
            <td v-if="IncludeMetadata == true">
                {{engineerUpdated | moment}}
            </td>
        </tr>

        <tr>
            <td><b>Heavy Assault</b></td>
            <td>
                {{timeAs.heavy | mduration}}
            </td>
            <td>
                {{score.heavy | locale}}
            </td>
            <td>
                {{score.heavy / (timeAs.heavy || 1) * 60 | fixed | locale}}
            </td>
            <td v-if="IncludeMetadata == true">
                {{heavyUpdated | moment}}
            </td>
        </tr>

        <tr>
            <td><b>MAX</b></td>
            <td>
                {{timeAs.max | mduration}}
            </td>
            <td>
                {{score.max | locale}}
            </td>
            <td>
                {{score.max / (timeAs.max || 1) * 60 | fixed | locale}}
            </td>
            <td v-if="IncludeMetadata == true">
                {{maxUpdated | moment}}
            </td>
        </tr>
    </table>
</template>

<script lang="ts">
    import Vue, { PropType } from "vue";

    import InfoHover from "components/InfoHover.vue";

    import { CharacterStat } from "api/CharacterStatApi";

    class ClassStatSet {
        public infil: number = 0;
        public lightAssault: number = 0;
        public medic: number = 0;
        public engineer: number = 0;
        public heavy: number = 0;
        public max: number = 0;
    }

    export const CharacterClassStats = Vue.extend({
        props: {
            data: { type: Array as PropType<CharacterStat[]>, required: true },
            type: { type: String, required: true },
            title: { type: String, required: true },
            IncludeMetadata: { type: Boolean, required: true },
        },

        mounted: function(): void {
            this.setData();
        },

        data: function() {
            return {
                kills: new ClassStatSet() as ClassStatSet,
                score: new ClassStatSet() as ClassStatSet,
                timeAs: new ClassStatSet() as ClassStatSet,

                infilUpdated: new Date() as Date,
                lightAssaultUpdated: new Date() as Date,
                medicUpdated: new Date() as Date,
                engineerUpdated: new Date() as Date,
                heavyUpdated: new Date() as Date,
                maxUpdated: new Date() as Date,
            }
        },

        methods: {

            setData: function(): void {
                if (this.type != "daily" && this.type != "weekly" && this.type != "monthly" && this.type != "forever") {
                    throw `Invalid type '${this.type}' passed, expected daily | weekly | monthly | forever`;
                }

                function setStat(set: ClassStatSet, stat: CharacterStat, type: string): void {
                    const now: Date = new Date();

                    let value: number;
                    if (type == "daily") {
                        value = stat.valueDaily;
                    } else if (type == "weekly") {
                        value = stat.valueWeekly;
                    } else if (type == "monthly") {
                        console.log(`${stat.timestamp.getFullYear()} ${stat.timestamp.getMonth()} ${now.getFullYear()} ${now.getMonth()}`);
                        // Value is too old to be useful here
                        if (stat.timestamp.getFullYear() != now.getFullYear() || stat.timestamp.getMonth() != now.getMonth()) {
                            return;
                        }
                        value = stat.valueMonthly;
                    } else if (type == "forever") {
                        value = stat.valueForever;
                    } else {
                        throw ``;
                    }

                    if (stat.profileID == 0) { // all

                    } else if (stat.profileID == 1) { // infil
                        set.infil = value;
                    } else if (stat.profileID == 3) { // light assault
                        set.lightAssault = value;
                    } else if (stat.profileID == 4) { // medic
                        set.medic = value;
                    } else if (stat.profileID == 5) { // engineer
                        set.engineer = value;
                    } else if (stat.profileID == 6) { // heavy
                        set.heavy = value;
                    } else if (stat.profileID == 7) { // max
                        set.max = value;
                    }
                }

                for (const stat of this.data) {
                    if (stat.statName == "kills") {
                        setStat(this.kills, stat, this.type);
                    } else if (stat.statName == "play_time") {
                        setStat(this.timeAs, stat, this.type);
                    } else if (stat.statName == "score") {
                        setStat(this.score, stat, this.type);
                    }

                    if (stat.profileID == 1) {
                        this.infilUpdated = stat.timestamp;
                    } else if (stat.profileID == 3) {
                        this.lightAssaultUpdated = stat.timestamp;
                    } else if (stat.profileID == 4) {
                        this.medicUpdated = stat.timestamp;
                    } else if (stat.profileID == 5) {
                        this.engineerUpdated = stat.timestamp;
                    } else if (stat.profileID == 6) {
                        this.heavyUpdated = stat.timestamp;
                    } else if (stat.profileID == 7) {
                        this.maxUpdated = stat.timestamp;
                    }
                }
            }
        },

        components: {
            InfoHover
        }
    });
    export default CharacterClassStats;

</script>