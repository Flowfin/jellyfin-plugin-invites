# Workflow parity, as a fixture

Deliberately small. What this file is for is the comparison, not the reasons.

## What the target gate has

| Workflow | Answer | Reason |
| --- | --- | --- |
| `headless.yml` | declined | A name that is in both tables, so the second table is what answers for this tree. |

## What this repository has

| Workflow | Answer | Reason |
| --- | --- | --- |
| `build.yaml` | kept | The build leg. |
| `fuzz.yaml` | kept | The fuzzing leg. |
| `headless.yaml` | kept | The suite with no network interface. |
| `sync-labels.yaml` | removed, here | It replaces this repository's labels with a shared list. |

## What is not in either table

Prose after the section, so the reader of this fixture can see the heading ends it.
