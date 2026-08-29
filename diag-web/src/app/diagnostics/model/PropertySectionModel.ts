import { SubBagModel } from './SubBagModel';

export class PropertySectionModel {
    readonly children: PropertySectionModel[] = [];
    subBag: SubBagModel | null = null;
    isExpanded = false;
    isExpandedProperty = false;
    hasExpandedAncestor = false;

    constructor(
        readonly name: string | null,
        readonly path: string,
        readonly depth: number
    ) {}

    static create(subBags: readonly SubBagModel[]): PropertySectionModel {
        const root = new PropertySectionModel(null, '', 0);

        for (const subBag of subBags) {
            const name = subBag.name();
            if (!name) {
                root.subBag = subBag;
                continue;
            }

            let parent = root;
            for (const part of name.split('.').filter(Boolean)) {
                const path = parent.path ? `${parent.path}.${part}` : part;
                let section = parent.children.find((child) => child.path === path);
                if (!section) {
                    section = new PropertySectionModel(part, path, parent.depth + 1);
                    parent.children.push(section);
                }
                parent = section;
            }
            parent.subBag = subBag;
            parent.isExpanded = subBag.isExpanded();
            parent.isExpandedProperty = subBag.isExpandedProperty();
        }

        root.children.sort((left, right) => Number(left.isExpanded) - Number(right.isExpanded));
        PropertySectionModel.markExpandedAncestors(root);

        return root;
    }

    private static markExpandedAncestors(section: PropertySectionModel, hasExpandedAncestor = false): void {
        section.hasExpandedAncestor = hasExpandedAncestor;
        for (const child of section.children) {
            PropertySectionModel.markExpandedAncestors(child, hasExpandedAncestor || section.isExpandedProperty);
        }
    }
}
