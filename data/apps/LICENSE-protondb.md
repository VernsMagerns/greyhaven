# ProtonDB summary: Open Database License

`protondb-summary.json.gz` is a database derived from ProtonDB's public report dumps
(https://github.com/bdefore/protondb-data), which ProtonDB makes available under the
**Open Database License (ODbL) 1.0** (https://opendatacommons.org/licenses/odbl/1-0/), with
individual contents under the Database Contents License
(https://opendatacommons.org/licenses/dbcl/1-0/).

This derived database is likewise made available under the ODbL 1.0. Contains information
from ProtonDB (https://www.protondb.com).

How it was made: `tools/apps/import.py protondb <dump>` counts, for each Steam app ID, the
reports from the two years before the dump's date, the share whose verdict is "works", and the
share that worked without tweaks. Nothing else from the reports is kept. The file records which
dump it came from.
