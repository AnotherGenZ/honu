﻿import Vue, { VNode, VNodeData, CreateElement } from "vue";

//import DateTimePicker from "components/DateTimePicker.vue";
import { Loading, Loadable } from "Loading";

const ValidFilterTypes: string[] = ["string", "number", "date"];

interface ConditionSettings {
    title: string;
    icon: string;
    color: string;
};

const Conditions: Map<string, ConditionSettings> = new Map([
    ["equals", { title: "Equals", icon: "fa-equals", color: "primary" }],
    ["not_equal", { title: "Not equal", icon: "fa-not-equal", color: "warning" }],
    ["less_than", { title: "Less than", icon: "fa-less-than", color: "info" }],
    ["greater_than", { title: "Greater than", icon: "fa-greater-than", color: "success" }],
    ["contains", { title: "Contains", icon: "fa-tilde", color: "info" }],
    ["not_empty", { title: "Not empty", icon: "fa-circle", color: "info" }],
    ["empty", { title: "Empty", icon: "fa-empty-set", color: "info" }]
]);

interface Header {
    empty: boolean;
    colClass: string;
    children: VNode[] | undefined;
    field: string | undefined;
};

interface Filter {
    value: any;

    selectedCondition: string;
    colClass: string;
    placeholder: string | undefined;

    conditions: string[];
    method: string;
    type: string;
    field: string;

    source: undefined | FilterKeyValue[];
    sourceKey: string | undefined;
    sourceValue: string | undefined;

    width: string | undefined;
    vnode: VNode | undefined;
};

export type FilterKeyValue = {
    key: string,
    value: any
}

export const ATable = Vue.extend({
    props: {
        // Where the data comes from
        source: { type: Function, required: true },

        // After data is bound this function is called
        PostProcess: { type: Function, required: false, default: undefined },

        // Will the header be displayed?
        ShowHeader: { type: Boolean, required: false, default: true },

        // Will the filters be displayed?
        ShowFilters: { type: Boolean, required: false, default: false },

        // Will the footer be displayed?
        ShowFooter: { type: Boolean, required: false, default: false },

        // Will data be split up into many pages?
        paginate: { type: Boolean, required: false, default: true },

        // How much padding will each row get
        RowPadding: { type: String, required: false, default: "normal" }, // "compact" | "normal" | "expanded"

        // Will the <a-table> be displayed as a <div>.list-group or a <table>
        DisplayType: { type: String, required: false, default: "list" }, // "list" | "table",

        // Field to sort on by default, if undefined goes to first <a-col> with a sort-field
        DefaultSortField: { type: String, required: false, default: undefined },

        // Order to sort by default
        DefaultSortOrder: { type: String, required: false, default: "asc" }
    },

    data: function() {
        return {
            nodes: {
                columns: [] as VNode[],
                headers: [] as Header[]
            },

            entries: Loadable.idle() as Loading<any[]>,

            sorting: {
                field: "" as string,
                type: "unknown" as "string" | "number" | "date" | "unknown",
                order: "asc" as "asc" | "desc"
            },

            filters: [] as Filter[],

            paging: {
                size: 50 as number,
                page: 0 as number
            },
        }
    },

    created: function(): void {
        this.bindData();

        this.nodes.columns = (this.$slots["default"] || [])
            .filter((iter: VNode) => iter.tag != undefined && iter.componentOptions != undefined);

        console.log(`<a-table>: Found ${this.nodes.columns.length} columns`);

        if (this.DefaultSortField != undefined) {
            this.sorting.field = this.DefaultSortField;
        }
        if (this.DefaultSortOrder == "asc" || this.DefaultSortOrder == "desc") {
            this.sorting.order = this.DefaultSortOrder;
        } else {
            throw `default-sort-order must be 'asc' | 'desc': Is '${this.DefaultSortOrder}'`;
        }

        // Iterate through all the columns and find any <a-header> elements
        for (const column of this.nodes.columns) {
            if (column.componentOptions?.children) {
                // Finding the <a-header> elements
                const headerNodes: VNode[] = column.componentOptions.children
                    .filter((iter: VNode) => iter.componentOptions?.tag == "a-header");

                // Ensure at most one exists for each column, cannot have multiple
                if (headerNodes.length > 1) {
                    throw "Cannot define multiple <a-header> elements in a single <a-col>";
                }

                // Get the HTML class used to define the columns
                const colClass: string = (column.componentOptions!.propsData as any).ColClass;
                const sortField: string | undefined = (column.componentOptions.propsData as any).SortField;

                const options: VNodeData = {};
                options.staticClass = colClass;

                if (sortField != undefined) {
                    options.on = {
                        click: this.sortTable.bind(this, sortField)
                    }
                }

                const header: Header = {
                    colClass: colClass,
                    field: sortField,
                    empty: true,
                    children: []
                };

                // A child <a-header> exists, use those options given
                if (headerNodes.length == 1) {
                    header.children = headerNodes[0].componentOptions?.children ?? [];
                    header.empty = false;

                    // Sort by the first field set
                    if (sortField != undefined && this.sorting.field == "") {
                        this.sorting.field = sortField;
                    }
                }

                this.nodes.headers.push(header);
            }
        }

        // Find the filters and create them
        for (const column of this.nodes.columns) {
            if (column.componentOptions?.children) {
                const filterNodes: VNode[] = column.componentOptions.children
                    .filter((iter: VNode) => iter.componentOptions?.tag == "a-filter");

                if (filterNodes.length > 1) {
                    throw `Cannot define multiple <a-filter> elements in a single <a-col>`;
                }

                const colClass: string = (column.componentOptions!.propsData as any).ColClass;

                const filter: Filter = {
                    method: "empty",
                    type: "empty",
                    conditions: [],
                    selectedCondition: "",
                    colClass: colClass,
                    field: "",
                    value: "",
                    source: undefined,
                    sourceKey: undefined,
                    sourceValue: undefined,
                    placeholder: undefined,
                    vnode: undefined,
                    width: undefined
                };

                if (filterNodes.length == 0) {
                    this.filters.push(filter);
                    continue;
                }

                const filterNode: VNode = filterNodes[0];
                filter.vnode = filterNode;

                filter.width = (filterNode.componentOptions!.propsData as any).MaxWidth;

                // Validate method prop
                filter.method = (filterNode.componentOptions!.propsData as any).method;
                if (typeof filter.method != "string") {
                    throw `Needed string for method of <a-filter>, got ${typeof filter.method}`;
                }
                if (filter.method == "reset") {
                    this.filters.push(filter);
                    continue;
                }

                // Validate a correct source for a dropdown filter
                if (filter.method == "dropdown") {
                    filter.source = (filterNode.componentOptions!.propsData as any).source;

                    console.log(`source found: ${filter.source}`, filter.source);

                    // A function was passed as the source, validate and begin the
                    if (typeof (filter.source) == "function") {
                        filter.sourceKey = (filterNode.componentOptions!.propsData as any).SourceKey;
                        if (filter.sourceKey == undefined) {
                            throw `Missing source-key for <a-filter>`;
                        }

                        filter.sourceValue = (filterNode.componentOptions!.propsData as any).SourceValue;
                        if (filter.sourceValue == undefined) {
                            throw `Missing source-value for <a-filter>`;
                        }

                        const sourceRet: any = (filter.source as Function)();
                        if (typeof (sourceRet.ok) != "function") {
                            throw `Missing ok callback handler or is not a function. Did you pass a function that returns an ApiResponse?`;
                        }

                        console.log(`filter source is a Function()`);

                        /*
                        const response: ApiResponse<object[]> = sourceRet as ApiResponse<object[]>;

                        response.ok((data: any[]) => {
                            if (data.length == 0) {
                                console.warn(`<a-table>: No objects returned from ApiResponse`);
                            } else {
                                const obj: object = data[0];

                                // Force is safe, checked above
                                if (obj.hasOwnProperty(filter.sourceKey!) == false) {
                                    throw `Bad source-key: ${filter.sourceKey} is not a property of data: ${JSON.stringify(obj)}`;
                                }
                                if (obj.hasOwnProperty(filter.sourceValue!) == false) {
                                    throw `Bad source-value: ${filter.sourceValue} is not a property of data: ${JSON.stringify(obj)}`;
                                }

                                filter.source = [];
                                for (const iter of data) {
                                    filter.source.push({
                                        key: iter[filter.sourceKey!],
                                        value: iter[filter.sourceValue!]
                                    });
                                }

                                filter.source.unshift({
                                    key: "All",
                                    value: null
                                });
                            }
                        });
                        */
                    } else if (filter.source != undefined && Array.isArray(filter.source) == false) {
                        throw `<a-filter> source was given but was not an array`;
                    }
                }

                // Validate type prop
                filter.type = (filterNode.componentOptions!.propsData as any).type;
                if (typeof filter.type != "string") {
                    throw `Needed string for type of <a-filter>, got ${typeof filter.type}`;
                }
                if (ValidFilterTypes.indexOf(filter.type) == -1) {
                    throw `Invalid filter type '${filter.type}'`;
                }

                // Validate conditions prop
                filter.conditions = (filterNode.componentOptions!.propsData as any).conditions;
                if (!Array.isArray(filter.conditions)) {
                    throw `Needed array for conditions of <a-filter>, got ${typeof filter.conditions}`;
                }
                if (filter.conditions.length == 0) {
                    throw `No conditions of <a-filter> given, need at least one`;
                }

                // Validate field prop
                filter.field = (filterNode.componentOptions!.propsData as any).field;
                if (typeof filter.field != "string") {
                    throw `Need string for field of <a-filter>, got ${typeof filter.field}`;
                }

                filter.placeholder = (filterNode.componentOptions!.propsData as any).placeholder;

                filter.value = (filter.type == "number") ? null : "";
                filter.selectedCondition = filter.conditions[0];

                this.filters.push(filter);
            }
        }
    },

    render: function(createElement: CreateElement): VNode {
        let rows: VNode[] = [];

        if (this.ShowHeader == true) {
            rows.push(this.renderHeader(createElement));
        }

        if (this.ShowFilters == true) {
            rows.push(this.renderFilter(createElement));
        }

        if (this.entries.state == "idle") {

        } else if (this.entries.state == "loading") {
            if (this.DisplayType == "table") {
                rows.push(createElement("tr", [
                    createElement("td", { attrs: { "colspan": `${this.nodes.columns.length}` } }, ["Loading..."])
                ]));
            } else {
                rows.push(createElement("div", { staticClass: "list-group-item" }, ["Loading..."]));
            }

            this.$emit("rerender", Loadable.loading());
        } else if (this.entries.state == "loaded") {
            for (const elem of this.displayedEntries) {
                rows.push(this.renderDataRow(createElement, elem));
            }

            this.$emit("rerender", Loadable.loaded(this.displayedEntries));
        } else if (this.entries.state == "error") {
            if (this.DisplayType == "table") {

            } else {

            }
            rows.push(createElement("div",
                {
                    staticClass: "list-group-item list-group-item-danger"
                },
                [`Error loading data from source: ${this.entries.message}`]
            ));

            this.$emit("rerender", Loadable.error(this.entries.message));
        } else {
            if (this.DisplayType == "table") {

            } else {

            }
            rows.push(createElement("div",
                {
                    staticClass: "list-group-item list-group-item-danger"
                },
                [`Unchecked state of entries: '${this.entries.state}'`]
            ));
        }

        if (this.paginate == true) {
            rows.push(this.renderPages(createElement));
        }

        if (this.DisplayType == "list") {
            return createElement("div",
                {
                    staticClass: "list-group a-table",
                    class: {
                        "list-group-small": (this.RowPadding == "compact")
                    }
                },
                rows
            );
        } else {
            return createElement("table", { staticClass: "table a-table" }, rows);
        }
    },

    methods: {
        createIcon: function(createElement: CreateElement, icon: string, style: string = "fas"): VNode {
            return createElement("span", { staticClass: `${style} fa-fw ${icon}` });
        },

        bindData: function(skipLoading: boolean = false): void {
            if (skipLoading == false) {
                this.entries = Loadable.loading();
            }

            const timer: number = new Date().getTime();
            this.source().then((data: object[]) => {
                const nowMs: number = new Date().getTime();
                console.log(`<a-table>: Took ${nowMs - timer}ms to load`);
                if (this.PostProcess != undefined) {
                    for (let i = 0; i < data.length; ++i) {
                        data[i] = this.PostProcess(data[i]);
                    }
                    console.log(`<a-table>: Took ${new Date().getTime() - nowMs}ms to post process ${data.length} entries`);
                }

                this.entries = Loadable.loaded(data);
            });
        },

        setPage: function(page: number): void {
            if (page > this.pageCount - 1) {
                this.paging.page = this.pageCount - 1;
            } else if (page <= 0) {
                this.paging.page = 0;
            } else {
                this.paging.page = page;
            }
        },

        sortTable: function(field: string): void {
            console.log(`<a-table>: sorting ATable on field '${field}'`);

            // Toggle between the two orders if the currently selected field is the same
            if (this.sorting.field == field) {
                if (this.sorting.order == "asc") {
                    this.sorting.order = "desc";
                } else {
                    this.sorting.order = "asc";
                }
                return;
            }

            // Can we check the type to update the sorting function?
            if (this.entries.state == "loaded" && this.entries.data.length > 0) {
                const first: any = this.entries.data[0];
                if (!first.hasOwnProperty(field)) {
                    throw `Cannot sort on field '${field}', not a property of ${JSON.stringify(first)}`;
                }

                let type: string = typeof first[field];
                if (type == "object") {
                    if (first[field] instanceof Date) {
                        type = "date";
                    }
                }

                if (type == "string") {
                    this.sorting.type = "string";
                } else if (type == "number") {
                    this.sorting.type = "number";
                } else if (type == "date") {
                    this.sorting.type = "date";
                } else {
                    throw `Unchecked sorting type: ${type}. Expected 'string' | 'number' | 'date'`;
                }
            } else {
                // We don't know what type the field is, it will be found on next render
                this.sorting.type = "unknown";
            }

            // Else begin sorting by this field
            this.sorting.field = field;
        },

        renderHeader(createElement: CreateElement): VNode {
            let headers: VNode[] = [];

            // Iterate through all the columns and find any <a-header> elements
            for (const header of this.nodes.headers) {
                const options: VNodeData = {};
                options.staticClass = header.colClass;

                if (header.field != undefined) {
                    options.on = {
                        click: this.sortTable.bind(this, header.field)
                    }
                }

                if (header.empty == false) {
                    const tagType: string = (this.DisplayType == "table") ? "td" : "div";
                    if (this.DisplayType == "table") {
                        options.staticClass = "";
                    }

                    headers.push(createElement(tagType, options, [
                        header.children,
                        (header.field != undefined) ? this.createSortable(createElement, header.field) : []
                    ]));
                } else {
                    // No <a-header> exists, input an empty col so the table stays lined up
                    if (this.DisplayType == "table") {
                        headers.push(createElement("td", options));
                    } else {
                        headers.push(createElement("div", options));
                    }
                }
            }

            // Return the .list-group-item for the header along with all of the headers set
            return createElement(
                (this.DisplayType == "table") ? "tr" : "div",
                {
                    staticClass: `a-table-header-row ${this.DisplayType == "table" ? "" : "row"}`
                },
                headers
            );
        },

        renderPages(createElement: CreateElement): VNode {
            if (this.DisplayType == "table") {
                return createElement("tr", [
                    createElement("td",
                        {
                            attrs: {
                                "colspan": `${this.nodes.columns.length}`
                            }
                        },
                        this.createPageButtons(createElement)
                    )
                ]);
            } else {
                return createElement("div",
                    {
                        staticClass: "list-group-item list-group-item-secondary"
                    },
                    this.createPageButtons(createElement)
                );
            }
        },

        renderDataRow(createElement: CreateElement, data: object): VNode {
            const cols: VNode[] = [];

            for (const column of this.nodes.columns) {
                if (column.componentOptions?.children) {
                    // Finding the <a-header> elements
                    const headerNodes: VNode[] = column.componentOptions.children
                        .filter((iter: VNode) => iter.componentOptions?.tag == "a-body");

                    if (headerNodes.length > 1) {
                        throw `Cannot have multiple <a-body>s per <a-col>`;
                    }

                    const colClass: string = (column.componentOptions!.propsData as any).ColClass;

                    if (headerNodes.length == 0) {
                        cols.push(createElement("div", { staticClass: colClass }));
                        continue;
                    }

                    const bodyNode: VNode = headerNodes[0];
                    if (bodyNode.data?.scopedSlots == undefined) {
                        throw `No slots defined for an <a-body>`;
                    }

                    const slot = bodyNode.data?.scopedSlots["default"];
                    if (slot == undefined) {
                        throw `Missing default slot for a <a-body>`;
                    }

                    let lineHeight: string = "1.5";
                    switch (this.RowPadding) {
                        case "compact": lineHeight = "1"; break;
                        case "expanded": lineHeight = "2"; break;
                        case "tiny": lineHeight = "0.8"; break;
                        default: lineHeight = "1.5"; break;
                    }

                    const options: VNodeData = {
                        staticClass: colClass,
                        staticStyle: {
                            "line-height": lineHeight
                        }
                    }

                    // Copy listeners to the generated node
                    if (bodyNode.componentOptions?.listeners) {
                        options.on = { ...bodyNode.componentOptions.listeners };
                    }

                    if (this.DisplayType == "table") {
                        options.staticClass = "";
                        cols.push(createElement("td", options, [slot(data)]));
                    } else {
                        cols.push(createElement("div", options, [slot(data)]));
                    }
                }
            }

            if (this.DisplayType == "table") {
                return createElement("tr", cols);
            }

            return createElement("div",
                { staticClass: "list-group-item" },
                [
                    createElement("div", { staticClass: "row align-items-center" }, cols)
                ]
            );
        },

        renderFilter(createElement: CreateElement): VNode {
            let filters: VNode[] = [];

            for (const filter of this.filters) {
                let inputNode: VNode;

                // Ensure the source is only populated once
                if (filter.method == "dropdown" && filter.source == undefined && this.entries.state == "loaded") {
                    console.log(`<a-filter:dropdown> no src, can load from data`);

                    // Find all unique value of the filter's field
                    // https://stackoverflow.com/questions/11246758
                    const uniqueFields: FilterKeyValue[] = this.entries.data
                        .map((iter: any) => iter[filter.field]) // Collect the field
                        .filter((iter, i, ar) => ar.indexOf(iter) == i) // Find unique
                        .map((iter) => ({ key: iter, value: iter })); // Map to the pair

                    // Include an all field
                    uniqueFields.unshift({ key: "All", value: null });

                    filter.source = uniqueFields;
                }

                // Create the appropriate <input> element 
                if (filter.method == "input") {
                    inputNode = this.createInputFilter(createElement, filter);
                } else if (filter.method == "dropdown") {
                    inputNode = this.createDropdownFilter(createElement, filter);
                } else if (filter.method == "reset") {
                    inputNode = this.createResetFilter(createElement, filter);
                } else if (filter.method == "empty") {
                    inputNode = createElement("div");
                } else {
                    throw `Unknown filter method '${filter.method}'`;
                }

                // Filter column wrapped in the .input-group
                if (this.DisplayType == "table") {
                    filters.push(createElement("td",
                        { staticStyle: { "max-width": filter.width ?? "auto" } },
                        [inputNode]));
                } else {
                    filters.push(createElement("div",
                        { staticClass: filter.colClass },
                        [inputNode]
                    ));
                }
            }

            if (this.DisplayType == "table") {
                // <tr> containing all the filter columns
                return createElement("tr", filters);
            } else {
                // .list-group-item containing all the filter columns
                return createElement("div",
                    { staticClass: "list-group-item list-group-item-secondary" },
                    [
                        createElement("div", { staticClass: "row align-items-center" }, filters)
                    ]
                );
            }
        },

        createInputFilter(createElement: CreateElement, filter: Filter): VNode {
            let input: VNode;

            if (filter.type == "string") {
                input = createElement("input", {
                    staticClass: "form-control a-table-filter-input",
                    attrs: {
                        "type": "text",
                        "placeholder": (filter.placeholder != undefined) ? filter.placeholder : ""
                    },
                    domProps: {
                        value: filter.value,
                    },
                    on: {
                        input: (ev: Event): void => {
                            filter.value = (ev.target as any).value;
                        }
                    }
                });
            } else if (filter.type == "number") {
                input = createElement("input", {
                    staticClass: "form-control a-table-filter-input",
                    domProps: {
                        value: filter.value,
                    },
                    attrs: {
                        "type": "text",
                        "placeholder": (filter.placeholder != undefined) ? filter.placeholder : ""
                    },
                    on: {
                        keydown: function(ev: KeyboardEvent): void {
                            const num: number = parseFloat(ev.key);
                            if (Number.isNaN(num)) {
                                // Keys like backspace give a key of "Backspace", while letters give single characters
                                if (ev.key.length == 1) {
                                    ev.preventDefault();
                                }
                            }
                        },
                        input: function(ev: InputEvent): void {
                            const value: string = (ev.target as HTMLInputElement).value;
                            if (value == "") {
                                filter.value = null;
                            } else {
                                const num: number = Number.parseFloat(value);
                                if (!Number.isNaN(num)) {
                                    filter.value = num;
                                }
                            }
                        }
                    }
                });
            } else if (filter.type == "date") {
                /*
                input = createElement(DateTimePicker, {
                    staticClass: "a-table-filter-date",
                    props: {
                        AllowNull: true,
                    },
                    scopedSlots: {
                        // <date-time-picker> uses the default slot to allow additional buttons to be added to
                        //      the calendar icon it uses
                        default: (): VNode => {
                            return this.createFilterConditionButton(createElement, filter);
                        }
                    },
                    on: {
                        // Because the DateTimePicker is generating the event, ev is a string, not an Event
                        input: (ev: string | null): void => {
                            filter.value = (ev == null) ? null : new Date(ev);
                        }
                    }
                });
                */
                throw `Type 'date' currently broken`;
            } else {
                throw `Unknown type: '${filter.type}'`;
            }

            if (filter.type == "number" || filter.type == "string") {
                // .input-group that wraps the conditions a filter can use in a Bootstrap dropdown
                return createElement("div",
                    {
                        staticClass: "input-group",
                    },
                    [
                        input, // Input being wrapped
                        this.createFilterCondition(createElement, filter) // Buttons to swap between conditions
                    ]
                );
            } else if (filter.type == "date") {
                // Condition buttons are added using the default slot of the <date-time-picker>
                return input;
            } else {
                throw `Unchecked type: '${filter.type}'. Cannot create element`;
            }
        },

        createDropdownFilter(createElement: CreateElement, filter: Filter): VNode {
            console.log(`Dropdown filter: `, filter.source);
            return createElement("select",
                {
                    staticClass: "form-control a-table-filter-select",
                    on: {
                        input: (event: InputEvent): void => {
                            if (filter.type == "number") {
                                filter.value = Number.parseFloat((event.target as any).value);
                            } else if (filter.type == "string") {
                                filter.value = ((event.target) as any).value;
                            } else {
                                throw `Cannot update the value for an <a-filter>, unchecked type: '${filter.type}'`;
                            }
                        }
                    }
                },
                filter.source?.map((iter: any) => {
                    return createElement("option", { domProps: { value: iter.value } }, iter.key);
                })
            );
        },

        createResetFilter(createElement: CreateElement, filter: Filter): VNode {
            return createElement("button",
                {
                    staticClass: "btn btn-secondary",
                    on: {
                        click: () => {
                            for (const filter of this.filters) {
                                if (filter.type == "number") {
                                    filter.value = null;
                                } else if (filter.type == "string") {
                                    filter.value = "";
                                } else if (filter.type == "date") {
                                    filter.value = null;
                                } else {
                                    throw `Cannot reset the value for an <a-filter>, unchecked type: '${filter.type}'`;
                                }
                            }
                        }
                    }
                },
                ["Reset"]
            );
        },

        createPageButtons(createElement: CreateElement): VNode[] {
            const nodes: VNode[] = [
                // Page selection buttons
                createElement("div", { staticClass: "btn-group" }, [
                    // First page button
                    createElement("button",
                        {
                            staticClass: "btn btn-light",
                            domProps: {
                                type: "button"
                            },
                            on: {
                                click: (): void => { this.paging.page = 0; }
                            }
                        },
                        [this.createIcon(createElement, "fa-chevron-circle-left", "fas")]
                    ),

                    // Previous page button
                    createElement("button",
                        {
                            staticClass: "btn btn-light",
                            domProps: {
                                type: "button"
                            },
                            on: {
                                click: (): void => { this.setPage(this.paging.page - 1) }
                            }
                        },
                        [this.createIcon(createElement, "fa-chevron-left", "fas")]
                    ),

                    // Page selection buttons, show 10 max
                    [...Array(Math.min(this.pageCount, 10)).keys()] // Get [0, N]
                        .map(i => ++i) // Transform to [1, N + 1]
                        .filter(i => i + this.pageOffset <= this.pageCount) // Ignore those over page count
                        .map((index: number): VNode => {
                            return createElement("button",
                                {
                                    staticClass: "btn",
                                    class: {
                                        "btn-primary": this.paging.page + 1 == index + this.pageOffset,
                                        "btn-light": this.paging.page + 1 != index + this.pageOffset
                                    },
                                    on: {
                                        click: (): void => { this.setPage(index + this.pageOffset - 1) }
                                    }
                                },
                                [`${this.pageOffset + index}`]
                            )
                        }
                        ),

                    // Next page button
                    createElement("button",
                        {
                            staticClass: "btn btn-light",
                            domProps: {
                                type: "button"
                            },
                            on: {
                                click: (): void => { this.setPage(this.paging.page + 1) }
                            }
                        },
                        [this.createIcon(createElement, "fa-chevron-right", "fas")]
                    ),

                    // Last page button
                    createElement("button",
                        {
                            staticClass: "btn btn-light",
                            domProps: {
                                type: "button"
                            },
                            on: {
                                click: (): void => { this.setPage(this.pageCount - 1); }
                            }
                        },
                        [this.createIcon(createElement, "fa-chevron-circle-right", "fas")]
                    )]
                ),

                // Viewing text
                createElement("span",
                    `Viewing ${Math.min(this.displayedEntries.length, this.paging.size)}/${this.filteredEntries.length}
                        entries in ${this.pageCount} pages`),

                // Page size selector
                createElement("span", { staticClass: "float-right" }, [
                    "Page size:",

                    // Input to select the page size
                    createElement("select",
                        {
                            staticClass: "form-control w-auto d-inline-block",
                            staticStyle: {
                                "vertical-align": "middle"
                            },
                            domProps: {
                                value: this.paging.size
                            },
                            on: {
                                input: (ev: InputEvent): void => {
                                    this.paging.size = Number.parseInt((ev.target as any).value);
                                }
                            },
                        },
                        [
                            [5, 10, 25, 50, 100, 200].map((amount: number): VNode => {
                                return createElement("option",
                                    {
                                        domProps: {
                                            value: amount
                                        }
                                    },
                                    [`${amount}`]
                                )
                            })
                        ]
                    )
                ])
            ];

            return nodes;
        },

        createFilterConditionButton(createElement: CreateElement, filter: Filter): VNode {
            // .btn-group that contains the button for the currently selected condition
            //      as well as the dropdown to change the current filter condition
            return createElement("div", { staticClass: "btn-group" }, [
                // Button describing the currently selected condition
                createElement("button",
                    {
                        staticClass: `btn btn-${Conditions.get(filter.selectedCondition)!.color} a-table-filter-dropdown`,
                        staticStyle: {
                            "border-top-left-radius": "0rem",
                            "border-top-right-radius": "0.25rem",
                            "border-bottom-right-radius": "0.25rem",
                            "border-bottom-left-radius": "0rem",
                        },
                        domProps: {
                            "type": "button",
                            "title": Conditions.get(filter.selectedCondition)!.title
                        },
                        // There is no click listener because Bootstrap creates it when we have the data-toggle
                        attrs: {
                            "data-toggle": "dropdown"
                        }
                    },
                    [
                        createElement("span", {
                            staticClass: `fas ${Conditions.get(filter.selectedCondition)!.icon}`
                        })
                    ]
                ),

                // Dropdown menu that contains all the possible filter conditions
                createElement("div",
                    {
                        staticClass: "dropdown-menu dropdown-menu-right border border-dark py-0",
                        staticStyle: {
                            "min-width": "1rem"
                        }
                    },
                    // Dropdown item for each filter condition
                    filter.conditions.map((condition: string): VNode => {
                        const condIcon: string = Conditions.get(condition)!.icon;
                        const condTitle: string = Conditions.get(condition)!.title;
                        const condColor: string = Conditions.get(condition)!.color;

                        return createElement("button",
                            {
                                staticClass: `dropdown-item border-bottom border-dark text-white bg-${condColor}`,
                                on: {
                                    click: () => {
                                        filter.selectedCondition = condition;
                                    }
                                }
                            },
                            [
                                createElement("span", { staticClass: `fas fa-fw ${condIcon}` }),
                                condTitle
                            ]
                        )
                    })
                )
            ]);
        },

        createFilterCondition(createElement: CreateElement, filter: Filter): VNode | undefined {
            // Do not add the dropdown, as it doesn't make sense to toggle between 1 condition
            if (filter.conditions.length == 1) {
                return undefined;
            }

            // Parent element containing the .btn-group
            return createElement("div",
                { staticClass: "input-group-append" },
                [this.createFilterConditionButton(createElement, filter)]
            );
        },

        createSortable(createElement: CreateElement, fieldName: string): VNode {
            if (this.sorting.field == fieldName) {
                return createElement("span", {
                    staticClass: (this.sorting.order == "asc")
                        ? "fas fa-caret-square-up fa-fw mr-auto"
                        : "fas fa-caret-square-down fa-fw mr-auto",
                });
            }

            return createElement("span");
        },
    },

    computed: {
        filteredEntries: function(): object[] {
            if (this.entries.state != "loaded") {
                return [];
            }

            const enabledFilters: Filter[] = this.filters.filter((iter: Filter) => {
                if (iter.selectedCondition == "empty" || iter.selectedCondition == "not_empty") {
                    return true;
                }
                if (iter.type == "string") {
                    return (iter.value as string).trim().length > 0;
                } else if (iter.type == "number") {
                    return (iter.value != null && Number.isNaN(iter.value) == false);
                } else if (iter.type == "date") {
                    return (iter.value != null && iter.value != "");
                } else if (iter.type == "empty") {
                    return false;
                } else if (iter.type == "reset") {
                    return false;
                } else {
                    throw `Unchecked filter type: '${iter.type}'. Cannot check if enabled`;
                }
            });

            if (enabledFilters.length == 0) {
                return this.entries.data;
            }

            console.log(`Enabled filters:\n${enabledFilters.map(iter => `${iter.field} ${iter.type}: ${iter.value}`).join("\n")}`);

            const filterFuncs: ((iter: object) => boolean)[] = enabledFilters.map((iter: Filter) => {
                if (iter.type == "string") {
                    if (iter.selectedCondition == "equals") {
                        return ((elem: any) => elem[iter.field] == iter.value);
                    } else if (iter.selectedCondition == "contains") {
                        const valueLower: string = iter.value.toLowerCase();
                        return ((elem: any) => elem[iter.field]?.toLowerCase().indexOf(valueLower) > -1);
                    } else if (iter.selectedCondition == "empty") {
                        return ((elem: any) => !elem[iter.field])
                    } else if (iter.selectedCondition == "not_empty") {
                        return ((elem: any) => !!elem[iter.field]);
                    } else {
                        throw `Invalid condition ${iter.selectedCondition} for type 'string'`;
                    }
                } else if (iter.type == "number") {
                    if (iter.selectedCondition == "equals") {
                        return ((elem: any) => elem[iter.field] == iter.value);
                    } else if (iter.selectedCondition == "not_equal") {
                        return ((elem: any) => elem[iter.field] != iter.value);
                    } else if (iter.selectedCondition == "greater_than") {
                        return ((elem: any) => elem[iter.field] > iter.value);
                    } else if (iter.selectedCondition == "less_than") {
                        return ((elem: any) => elem[iter.field] < iter.value);
                    } else if (iter.selectedCondition == "empty") {
                        return ((elem: any) => elem[iter.field] == null || elem[iter.field] == undefined);
                    } else if (iter.selectedCondition == "not_empty") {
                        return ((elem: any) => elem[iter.field] != null && elem[iter.field] != undefined);
                    } else {
                        throw `Invalid condition ${iter.selectedCondition} for type 'number'`;
                    }
                } else if (iter.type == "date") {
                    const iterTime: number = (iter.value as Date).getTime();
                    if (iter.selectedCondition == "equals") {
                        return ((elem: any) => elem[iter.field]?.getTime() == iterTime);
                    } else if (iter.selectedCondition == "not_equal") {
                        return ((elem: any) => elem[iter.field]?.getTime() != iterTime);
                    } else if (iter.selectedCondition == "greater_than") {
                        return ((elem: any) => elem[iter.field]?.getTime() > iterTime);
                    } else if (iter.selectedCondition == "less_than") {
                        return ((elem: any) => elem[iter.field]?.getTime() < iterTime);
                    } else if (iter.selectedCondition == "empty") {
                        return ((elem: any) => elem[iter.field] == null || elem[iter.field] == undefined);
                    } else if (iter.selectedCondition == "not_empty") {
                        return ((elem: any) => elem[iter.field] != null && elem[iter.field] != undefined);
                    } else {
                        throw `Invalid condition ${iter.selectedCondition} for type 'date'`;
                    }
                }
                throw `Uncheck type to create a filter function for: '${iter.type}'`;
            });

            return this.entries.data.filter((iter: object) => {
                for (const func of filterFuncs) {
                    if (func(iter) == false) {
                        return false;
                    }
                }
                return true;
            });
        },

        displayedEntries: function(): object[] {
            if (this.entries.state != "loaded") {
                return [];
            }

            if (this.sorting.type == "unknown" && this.sorting.field != "") {
                const first: object = this.entries.data[0];
                if (!first.hasOwnProperty(this.sorting.field)) {
                    throw `Cannot sort on '${this.sorting.field}', is not in ${JSON.stringify(first)}`;
                }

                const obj: any = first;
                const val: any = obj[this.sorting.field];
                let type: string = typeof val;
                if (type == "object") {
                    if (val instanceof Date) {
                        type = "date";
                    }
                }

                if (type == "string") {
                    this.sorting.type = "string";
                } else if (type == "number") {
                    this.sorting.type = "number";
                } else if (type == "date") {
                    this.sorting.type = "date";
                } else {
                    throw `Unchecked type ${type} from field ${this.sorting.field}, expected 'string' | 'number' | 'date'`;
                }
            }

            let baseFunc: (a: object, b: object) => number = (a, b) => 1;
            let sortFunc: (a: object, b: object) => number;

            if (this.sorting.field != "") {
                if (this.sorting.type == "string") {
                    baseFunc = (a: any, b: any): number => {
                        const av: string = a[this.sorting.field];
                        const bv: string = b[this.sorting.field];

                        return av.localeCompare(bv);
                    }
                } else if (this.sorting.type == "number") {
                    baseFunc = (a: any, b: any): number => {
                        const av: number = a[this.sorting.field];
                        const bv: number = b[this.sorting.field];

                        return av - bv;
                    }
                } else if (this.sorting.type == "date") {
                    baseFunc = (a: any, b: any): number => {
                        const av: number = a[this.sorting.field].getTime();
                        const bv: number = b[this.sorting.field].getTime();

                        return av - bv;
                    }
                }
                else {
                    throw `Unchecked sorting type: '${this.sorting.type}'. Expected 'string' | 'number' | 'date'`;
                }
            }

            if (this.sorting.order == "desc") {
                sortFunc = (a: object, b: object): number => {
                    return -baseFunc(a, b); // Swap order for descending sort
                }
            } else {
                sortFunc = baseFunc;
            }

            if (this.paginate == true) {
                return this.filteredEntries
                    .sort(sortFunc)
                    .slice(this.paging.page * this.paging.size, (this.paging.page + 1) * this.paging.size);
            } else {
                return this.filteredEntries
                    .sort(sortFunc);
            }
        },

        pageCount: function(): number {
            if (this.entries.state != "loaded") {
                return 0;
            }

            return Math.ceil(this.filteredEntries.length / this.paging.size);
        },

        pageOffset: function(): number {
            return Math.floor(this.paging.page / 10) * 10;
        },
    }
});
export default ATable;

const ACol = Vue.extend({
    props: {
        ColClass: { type: String, required: false, default: "col-auto" },
        SortField: { type: String, required: false, default: undefined }
    },
    template: `<div></div>`
});

const AHeader = Vue.extend({
    template: `<div></div>`
});

const ABody = Vue.extend({
    template: `<div></div>`
});

const AFilter = Vue.extend({
    props: {
        method: { type: String, required: true },
        type: { type: String, required: true },
        conditions: { type: Array, required: true },
        field: { type: String, required: true },
        placeholder: { type: String, required: false, default: undefined },
        MaxWidth: { type: String, required: false, default: undefined },
        source: { type: Object, required: true },
        SourceKey: { type: String, required: false },
        SourceValue: { type: String, required: false }
    },
    template: `<div></div>`
});

export { ACol, AHeader, ABody, AFilter }