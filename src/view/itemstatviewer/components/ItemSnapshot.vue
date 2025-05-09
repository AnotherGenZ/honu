﻿<template>
    <div>

        <div class="d-flex flex-row">
            <toggle-button v-model="show.totalKills" class="flex-grow-1">
                Total kills
            </toggle-button>
            <toggle-button v-model="show.totalVKills" class="flex-grow-1">
                Total v. kills
            </toggle-button>
            <toggle-button v-model="show.totalDeaths" class="flex-grow-1">
                Total deaths
            </toggle-button>
            <toggle-button v-model="show.totalKd" class="flex-grow-1">
                Total KD
            </toggle-button>
            <toggle-button v-model="show.totalKpm" class="flex-grow-1">
                Total KPM
            </toggle-button>
            <toggle-button v-model="show.totalVkpm" class="flex-grow-1">
                Total VKPM
            </toggle-button>
            <toggle-button v-model="show.totalAcc" class="flex-grow-1">
                Total Accuracy
            </toggle-button>
            <toggle-button v-model="show.totalHsr" class="flex-grow-1">
                Total HSR
            </toggle-button>
            <toggle-button v-model="show.totalTime" class="flex-grow-1">
                Total time
            </toggle-button>
        </div>

        <div class="d-flex flex-row">
            <toggle-button v-model="show.intervalKills" class="flex-grow-1">
                Interval kills
            </toggle-button>
            <toggle-button v-model="show.intervalVKills" class="flex-grow-1">
                Interval v. kills
            </toggle-button>
            <toggle-button v-model="show.intervalDeaths" class="flex-grow-1">
                Interval deaths
            </toggle-button>
            <toggle-button v-model="show.intervalKd" class="flex-grow-1">
                Interval KD
            </toggle-button>
            <toggle-button v-model="show.intervalKpm" class="flex-grow-1">
                Interval KPM
            </toggle-button>
            <toggle-button v-model="show.intervalVkpm" class="flex-grow-1">
                Interval VKPM
            </toggle-button>
            <toggle-button v-model="show.intervalAcc" class="flex-grow-1">
                Interval Accuracy
            </toggle-button>
            <toggle-button v-model="show.intervalHsr" class="flex-grow-1">
                Interval HSR
            </toggle-button>
            <toggle-button v-model="show.intervalTime" class="flex-grow-1">
                Interval time
            </toggle-button>
        </div>

        <div>
            <toggle-button v-model="options.autoRedraw">
                Auto redraw
            </toggle-button>

            <button type="button" class="btn btn-primary" @click="makeGraph">
                Redraw
            </button>
        </div>

        <div id="hc-chart" style="height: 80vh; max-height: 80vh;" class="mb-2 w-100"></div>
    </div>
</template>

<script lang="ts">
    import Vue from "vue";
    import { Loading, Loadable } from "Loading";

    import { WeaponStatSnapshot, WeaponStatSnapshotApi } from "api/WeaponStatSnapshotApi";

    import * as hc from "highcharts";
    import * as hs from "highcharts/highstock";
    import "highcharts/modules/annotations";

    import TimeUtils from "util/Time";
    import ColorUtils from "util/Color";
    import LocaleUtil from "util/Locale";

    import "MomentFilter";

    import ToggleButton from "components/ToggleButton";

    type DiffedSnapshot = {
        id: number;
        timestamp: Date;

        overallKd: number;
        overallKpm: number;
        overallVkpm: number;
        overallAcc: number;
        overallHsr: number;

        intervalKd?: number;
        intervalKpm?: number;
        intervalVkpm?: number;
        intervalAcc?: number;
        intervalHsr?: number;

        kills: number;
        vkills: number;
        deaths: number;
        shots: number;
        shotsHit: number;
        headshots: number;
        secondsWith: number;

        killsDiff?: number;
        vkillsDiff?: number;
        deathsDiff?: number;
        shotsDiff?: number;
        shotsHitDiff?: number;
        headshotsDiff?: number;
        secondsDiff?: number;
    };

    type GraphableField = Omit<DiffedSnapshot, "timestamp" | "id">;

    const ValueToYAxis: Map<keyof GraphableField, number> = new Map([
        ["kills", 0],
        ["deaths", 1],
        ["shots", 2],
        ["shotsHit", 3],
        ["headshots", 4],
        ["secondsWith", 5],
        ["killsDiff", 6],
        ["deathsDiff", 7],
        ["shotsDiff", 8],
        ["shotsHitDiff", 9],
        ["headshotsDiff", 10],
        ["secondsDiff", 11],
        ["overallKd", 12],
        ["overallKpm", 13],
        ["overallAcc", 14],
        ["overallHsr", 15],
        ["intervalKd", 16],
        ["intervalKpm", 17],
        ["intervalAcc", 18],
        ["intervalHsr", 19],
        ["vkills", 20],
        ["vkillsDiff", 21],
        ["overallVkpm", 22],
        ["intervalVkpm", 23]
    ]);

    function createYAxis(name: string, visible: boolean, id: number): hc.YAxisOptions {
        return {
            min: 0,
            title: {
                text: name,
                style: {
                    color: "#ffffff"
                }
            },
            labels: {
                style: {
                    color: "#ffffff"
                }
            },
            visible: visible,
            id: `${id}`
        };
    }

    export const ItemSnapshot = Vue.extend({
        props: {
            ItemId: { type: String, required: true }
        },

        data: function() {
            return {
                snapshots: Loadable.idle() as Loading<WeaponStatSnapshot[]>,
                data: [] as DiffedSnapshot[],

                chart: null as hc.StockChart | null,

                options: {
                    autoRedraw: true as boolean,
                },

                show: {
                    totalKills: false as boolean,
                    totalVKills: false as boolean,
                    totalDeaths: false as boolean,
                    totalKd: false as boolean,
                    totalKpm: false as boolean,
                    totalVkpm: false as boolean,
                    totalAcc: false as boolean,
                    totalHsr: false as boolean,
                    totalTime: false as boolean,

                    intervalKills: false as boolean,
                    intervalVKills: false as boolean,
                    intervalDeaths: false as boolean,
                    intervalKd: true as boolean,
                    intervalKpm: true as boolean,
                    intervalVkpm: true as boolean,
                    intervalAcc: true as boolean,
                    intervalHsr: true as boolean,
                    intervalTime: true as boolean,
                }
            }
        },

        created: function(): void {
            this.bind();
        },

        methods: {
            bind: async function(): Promise<void> {
                this.data = [];

                this.snapshots = Loadable.loading();
                this.snapshots = await WeaponStatSnapshotApi.getByItemID(Number.parseInt(this.ItemId));

                if (this.snapshots.state == "loaded") {
                    const sorted: WeaponStatSnapshot[] = this.snapshots.data.sort((a, b) => a.timestamp.getTime() - b.timestamp.getTime());

                    this.data = sorted.map((iter: WeaponStatSnapshot, index: number) => {
                        let next: WeaponStatSnapshot | null = null;
                        if (index != sorted.length - 1) {
                            next = sorted[index + 1];
                        }

                        const datum: DiffedSnapshot = {
                            id: iter.itemID,
                            timestamp: iter.timestamp,

                            overallKd: iter.kills / Math.max(1, iter.deaths),
                            overallKpm: iter.kills / Math.max(1, iter.secondsWith) * 60,
                            overallVkpm: iter.vehicleKills / Math.max(1, iter.secondsWith) * 60,
                            overallAcc: iter.shotsHit / Math.max(1, iter.shots),
                            overallHsr: iter.headshots / Math.max(1, iter.kills),

                            kills: iter.kills,
                            vkills: iter.vehicleKills,
                            deaths: iter.deaths,
                            shots: iter.shots,
                            shotsHit: iter.shotsHit,
                            headshots: iter.headshots,
                            secondsWith: iter.secondsWith
                        };

                        if (next != null) {
                            datum.killsDiff = iter.kills - next.kills;
                            datum.vkillsDiff = iter.vehicleKills - next.vehicleKills;
                            datum.deathsDiff = iter.deaths - next.deaths;
                            datum.shotsDiff = iter.shots - next.shots;
                            datum.shotsHitDiff = iter.shotsHit - next.shotsHit;
                            datum.headshotsDiff = iter.headshots - next.headshots;
                            datum.secondsDiff = iter.secondsWith - next.secondsWith;

                            datum.intervalKd = datum.killsDiff / Math.max(1, datum.deathsDiff);
                            datum.intervalKpm = datum.killsDiff / Math.max(1, datum.secondsDiff) * 60;
                            datum.intervalVkpm = datum.vkillsDiff / Math.max(1, datum.secondsDiff) * 60;
                            datum.intervalAcc = datum.shotsHitDiff / Math.max(1, datum.shotsDiff);
                            datum.intervalHsr = datum.headshotsDiff / Math.max(1, datum.killsDiff);
                        }

                        return datum;
                    });
                }

                this.makeGraph();
            },

            makeGraph: function(): void {
                if (this.chart != null) {
                    this.chart.destroy();
                    this.chart = null;
                }

                const colors: string[] = ColorUtils.randomColors(Math.random(), 20);

                this.chart = hs.stockChart("hc-chart", {
                    title: { text: "" },

                    chart: {
                        zooming: {
                            type: "x"
                        },
                        backgroundColor: "#222222",
                    },

                    legend: {
                        itemStyle: { color: "#ffffff" }
                    },

                    xAxis: {
                        labels: {
                            formatter: (v) => {
                                const d: Date = new Date(v.value);
                                return TimeUtils.format(d);
                            },
                            style: { color: "#ffffff" }
                        },
                        crosshair: true
                    },

                    yAxis: [
                        createYAxis("", true, 0)
                    ],

                    tooltip: {
                        split: true,
                        headerFormat: "",
                        pointFormatter: function(): string {
                            const d: Date = new Date(this.x);

                            let ret: string = "<b>" + this.series.name + "</b><br/>";
                            ret += `${TimeUtils.format(d)}<br/>`;

                            const name: string = this.series.name;
                            if (name.indexOf("length") > -1 || name.indexOf("average") > -1) {
                                ret += `${TimeUtils.duration(this.y || 0)}</br>`;
                            } else {
                                ret += `${LocaleUtil.locale(this.y || 0)}<br/>`;
                            }

                            return ret;
                        }
                    },

                    series: this.generateSeries(this.data, colors, "stuff"),

                    annotations: [
                        {
                            crop: false,
                            labels: [
                                {
                                    point: { x: 0, y: 0, xAxis: 0, yAxis: 0 },
                                    text: "Honu starts",
                                },
                                {
                                    point: "max",
                                    text: "Max"
                                }
                            ]
                        }
                    ]
                });

                console.log(`created graph ${this.chart.series.length} series, ${this.chart.series.map(iter => iter.data.length).join(", ")}`);

            },

            generateSeries: function(entries: DiffedSnapshot[], colors: string[], name: string): hc.SeriesOptionsType[] {
                const arr: hc.SeriesOptionsType[] = [];

                const fields: (keyof GraphableField)[] = [
                    "kills", "vkills", "deaths", "secondsWith",
                    "killsDiff", "deathsDiff", "vkillsDiff",
                    "overallAcc", "overallHsr", "overallKd", "overallKpm", "overallVkpm",
                    "intervalKpm", "intervalKd", "intervalAcc", "intervalHsr", "intervalVkpm"
                ];

                for (let i = 0; i < fields.length; ++i) {
                    const field: keyof DiffedSnapshot = fields[i];

                    const yaxisIndex: number | undefined = ValueToYAxis.get(field);
                    if (yaxisIndex == undefined) {
                        throw `Missing y axis of field ${field}`;
                    }

                    const options: hc.SeriesOptionsType = {
                        type: "spline",
                        name: `${name} - ${field}`,
                        data: entries.map(iter => {
                            return {
                                x: iter.timestamp.getTime(),
                                y: iter[field] ?? 0
                            };
                        }),
                        color: colors[i % colors.length - 1],
                        turboThreshold: 1000000,
                        visible: this.isVisible(field),
                        id: field
                    };

                    console.log(`Options for ${field} is on index ${yaxisIndex}, showing? ${options.visible}, data: ${options.data?.length}`);

                    arr.push(options);
                }

                console.log(`data ${arr.map(iter => iter.data.length).join(", ")}`);

                return arr;
            },

            isVisible: function(field: keyof GraphableField): boolean {
                if (field == "kills") {
                    return this.show.totalKills;
                } else if (field == "vkills") {
                    return this.show.totalVKills;
                } else if (field == "deaths") {
                    return this.show.totalDeaths;
                } else if (field == "secondsWith") {
                    return this.show.totalTime;
                } else if (field == "killsDiff") {
                    return this.show.intervalKills;
                } else if (field == "vkillsDiff") {
                    return this.show.intervalVKills;
                } else if (field == "deathsDiff") {
                    return this.show.intervalDeaths;
                } else if (field == "overallKpm") {
                    return this.show.totalKpm;
                } else if (field == "intervalKpm") {
                    return this.show.intervalKpm;
                } else if (field == "secondsDiff") {
                    return this.show.intervalTime;
                } else if (field == "overallAcc") {
                    return this.show.totalAcc;
                } else if (field == "overallHsr") {
                    return this.show.totalHsr;
                } else if (field == "intervalAcc") {
                    return this.show.intervalAcc;
                } else if (field == "intervalHsr") {
                    return this.show.intervalHsr;
                } else if (field == "overallKd") {
                    return this.show.totalKd;
                } else if (field == "intervalKd") {
                    return this.show.intervalKd;
                } else if (field == "overallVkpm") {
                    return this.show.totalVkpm;
                } else if (field == "intervalVkpm") {
                    return this.show.intervalVkpm;
                }

                console.warn(`Unchecked field '${field}' when checking visiblity`);

                return true;
            },

            updateVisibility: function(): void {
                if (this.chart == null) {
                    return;
                }

                for (const series of this.chart.series) {
                    if (series.userOptions.id == undefined) {
                        console.warn(`missing userOptions.id?`);
                        continue;
                    }

                    const id: string = series.userOptions.id;
                    console.log(`updating visilbity of ${id} with ${series.data.length} data points`);

                    // not ideal
                    series.setVisible(this.isVisible(series.userOptions.id! as any));
                }

                this.chart.redraw();
            }
        },

        watch: {
            show: {
                deep: true,
                handler: function() {
                    if (this.options.autoRedraw == true) {
                        this.updateVisibility();
                    }
                }
            }
        },

        components: {
            ToggleButton

        }
    });
    export default ItemSnapshot;
</script>