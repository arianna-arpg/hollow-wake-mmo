# Skein hosting and scaling plan

| | |
|---|---|
| File | `docs/infrastructure/hosting.md` |
| Prepared | 2026-10-16 |
| Scope | Starter hosting for about 4 concurrent players, and a staged path to hundreds and thousands of players using zone-sharded server processes |
| Workload | C#/.NET authoritative server in Docker on Linux x64. One UDP port (RakNet), one TCP port, optional HTTP admin port. 20 to 30 Hz tick. One process can run several zones. PostgreSQL for persistence (SQLite or JSON files are acceptable for the first weeks). |
| Operators | A solo developer or a tiny team, comfortable with Docker and a terminal, who wants a small bill at the start |

**How to read the numbers.** Every price in this document was read on 2026-10-16 (the "date looked"). Cloud prices moved a lot in 2026: Hetzner repriced on 2026-06-15, Oracle halved its free Arm tier in mid-2026, and DigitalOcean changes its managed PostgreSQL editions from 2026-10-15. Re-check any figure before you commit money. Where I could not verify a price I say so and give no number.

Evidence legend used in the tables:

- **V**: verified on the provider's own page, documentation or public API on 2026-10-16.
- **S**: secondary source only (blog, aggregator, news article). Indicative, not authoritative.
- **U**: could not be verified. No number is asserted.
- Prices are USD per month, excluding tax, unless a cell says EUR. Hetzner quotes are net of VAT.
- Placeholder ports used below: UDP 27500 (RakNet), TCP 27501, HTTP admin 8080. Replace them with the real ones.

---

## 1. Recommendation

### 1.1 Verdict on the expectation

The expectation is right in shape. Two details need correcting and one needs a qualifier.

**Confirmed.** One small shared-vCPU VPS in the metro nearest the players, running Docker Compose with the Skein server and PostgreSQL on the same box, is the right starter for 4 players. The workload needs roughly 2 vCPU and 2 to 4 GB of RAM. The bill is the VM; bandwidth is negligible (about 25 GB a month at the assumptions in section 9).

**Corrected and qualified.**

1. **Hetzner Cloud is not a safe default for a new customer right now.** Hetzner's status page has carried an open notice since 2026-06-26 titled "Limited availability of cloud instances" (state: identified, not resolved). It says cloud servers can only be provisioned to a limited extent because of high demand and scarce hardware components, and that creation of new servers is restricted for new customers and for a randomly chosen group of existing customers. On 2026-10-16 the Cost-Optimized (CX, CAX) and Regular Performance (CPX) product pages marked every plan "currently unavailable". That label comes from the page text retrieved by an automated fetch on that date, and your console may show something different, so check it, but plan on the possibility that you cannot order a Hetzner server at all. Sources: [Hetzner status](https://status.hetzner.com/), [the notice](https://status.hetzner.com/incident/0a75c7ae-3377-41dc-aabe-601063724d24), [Cost-Optimized page](https://www.hetzner.com/cloud/cost-optimized/), [Regular Performance page](https://www.hetzner.com/cloud/regular-performance/), [Hetzner Cloud FAQ, last changed 2026-04-27](https://docs.hetzner.com/cloud/general/faq).
2. **Hetzner is no longer cheap in the US.** After the price adjustment effective 2026-06-15, the smallest US plan (CPX11: 2 vCPU, 2 GB, 1 TB traffic) is EUR 17.49 / $20.49 a month, and the US sites offer no CX or CAX plans at all. Vultr sells 2 vCPU / 4 GB / 3 TB for $20. Sources: [Hetzner price adjustment, last change 2026-07-08](https://docs.hetzner.com/general/infrastructure-and-availability/price-adjustment/), [Vultr plan API](https://api.vultr.com/v2/plans?type=vc2&per_page=25).
3. **Hetzner is still the best price in Europe on paper.** CX23 (2 vCPU, 4 GB, 40 GB NVMe, 20 TB traffic) costs EUR 5.49 plus EUR 0.50 for the IPv4 address, so EUR 5.99 a month net of VAT, if your account can create one. Sources: same price-adjustment page, [Primary IP price](https://docs.hetzner.com/cloud/servers/primary-ips/overview/).

### 1.2 Concrete starter choice

| Where most of the 4 players live | Starter | Spec | Monthly | Notes |
|---|---|---|---|---|
| North America | Vultr Cloud Compute Regular `vc2-2c-4gb`, in the offered metro with the lowest measured RTT from your players. US choices for this plan: New Jersey, Atlanta, Chicago, Dallas, Miami, Seattle, Los Angeles, Silicon Valley | 2 vCPU, 4 GB RAM, 80 GB SSD, 3 TB traffic | $20.00 (hourly $0.027) | Cheapest 2 vCPU / 4 GB plan I could verify at any provider with US sites ([Vultr API](https://api.vultr.com/v2/plans?type=vc2&per_page=25)) |
| Players on both US coasts | Same plan in Chicago or Dallas | same | $20.00 | Azure backbone median RTT is 28 to 29 ms Central US to East US and 39 ms Central US to West US ([Azure latency table](https://learn.microsoft.com/en-us/azure/networking/azure-network-latency)) |
| Europe | First choice: Hetzner Cloud CX23 in Falkenstein, Nuremberg or Helsinki. Fallback the same day: Vultr `vc2-2c-4gb` in Amsterdam, Frankfurt, London or Paris | 2 vCPU, 4 GB | EUR 5.99 net with IPv4 (Hetzner), or $20.00 (Vultr) | Try Hetzner first; if the console refuses to create it, take the fallback rather than waiting |
| Money is the only constraint | Vultr `vc2-1c-2gb` | 1 vCPU, 2 GB RAM, 55 GB, 2 TB | $10.00 | Fits 4 players plus Postgres, but leaves no room for monitoring agents or image pulls, and one shared vCPU gives the tick thread and GC less slack |

Why this is the right shape:

- **Hourly billing makes the choice cheap to test.** Vultr, Akamai/Linode, DigitalOcean and Hetzner all publish hourly rates, so you can create the same plan in two or three candidate metros, have each player run `ping` against it for an hour, and delete the losers. At $0.027 an hour the experiment costs cents.
- **Everything is portable.** Docker images, one Compose file and a bootstrap script (section 11) rebuild the box on any provider in about 30 minutes. Backups go to a different provider (section 8), so a provider problem never takes your data with it.
- **Bundled transfer covers you for a long time.** 4 players use about 25 to 60 GB a month; the plan includes 3 TB.
- **The pick rests on verified price, location coverage and bundled transfer, not on CPU benchmarks.** I have no benchmark data for any provider. Run the 24-hour jitter test from section 13 on the box you choose, and use hourly billing to try High Frequency `vhf-2c-4gb` ($24) or another provider if tail latency or CPU steal is bad.

### 1.3 Why not the other options (short form)

| Option | Why not first | Where it fits |
|---|---|---|
| DigitalOcean 2 vCPU / 4 GiB, $24 | Same shape, $4 more | Good if you expect to use DO Managed PostgreSQL or DOKS later |
| Akamai/Linode 4 GB (2 vCPU), $24 | Same shape, $4 more, 10 US regions | Fine alternative, cheapest overage ($0.005/GB) |
| AWS Lightsail $12 or $24 | CPU is burstable: baseline is 20% of each vCPU on both plans, which a steady tick loop can eventually exceed | Fine for the first weeks only |
| Oracle Always Free | Allowance was halved to 2 OCPU / 12 GB Arm in mid-2026; the idle-reclamation rule would match a 4-player server; Arm only, and the RakNet package in this repo has no linux-arm64 library (section 1.6); capacity errors | A free box for the pure-.NET TCP host or other side services |
| Fly.io | UDP works but needs a dedicated IPv4 and a special bind address, and the shared CPU baseline is 6.25%; a steady full vCPU costs about $36 a month | A later edge or short-lived instance role |
| Railway, Render | No inbound public UDP | HTTP side services only |
| Spare PC at home | Right only for a trusted group of up to 6 people on Tailscale; see section 4 | Weeks 0 to 4 if you want $0 |

### 1.4 Cost ladder at a glance

Detailed assumptions and arithmetic are in section 9. Ladder A uses Vultr list prices (orderable today). Ladder B uses Hetzner EU best-case prices after the June 2026 increase and only applies if Hetzner lets your account order servers again.

| Stage | Peak concurrent players | Shape | Ladder A (Vultr), per month | Ladder B (Hetzner EU, net), per month |
|---|---|---|---|---|
| 1 | 4 | One VPS, Compose, nightly dump off-provider | about $20 | about EUR 6 |
| 2 | 50 | One bigger VPS plus a staging box, PITR backups | about $50 | about EUR 16 |
| 3 | 500 | 3 game hosts, 1 DB host, control plane, monitoring, staging | about $225 | about EUR 57 |
| 4 | 5,000 | 16 game hosts, DB primary plus replica, 3 gateway/control hosts, monitoring, staging | about $1,160 (about $1,800 with dedicated vCPUs on game hosts) | about EUR 270 |

These are per region and exclude your own time, domain registration and any paid DDoS add-ons.

### 1.5 First-week checklist

1. Ask each player to run `ping -n 50` against two or three candidate regions' test instances and record average and worst case (section 10).
2. Create the winner, run the bootstrap (section 11), and deploy the Compose stack.
3. Set the nightly dump to an object-storage bucket at a different provider and do one restore drill before you rely on it (section 8).
4. Point a DNS name at the server and put the name, not the IP, in the client.
5. Write down the three numbers to measure next: tick time p99, CPU per player, bytes per player per second (section 13).

### 1.6 What the repository itself says (read-only inspection on 2026-10-16)

I looked at the working tree without changing it. At the time the host programs were placeholders ("host not wired yet"), so the ports, tick rate and configuration keys in this document are placeholders and every capacity figure is an assumption. Four findings do change the hosting advice:

1. **The RakNet host needs .NET 10.** `src/Skein.Server.Host.Brutal` builds on the `Ahwoo.Brutal.*` packages (2026.10.x). Its project file says they are .NET 10 only and falls back to a stub on older SDKs. Use a .NET 10 runtime image for that host. .NET 10 is the supported LTS (to 2028-11-14); .NET 8 ends on 2026-11-10 ([Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)).
2. **The other projects still target net8.0** (Core, Protocol, Client, Server, the pure-.NET TCP host, Tools and the tests), and `global.json` pins SDK 8.0.100 with `rollForward: latestMajor`. Retarget them to net10.0 before the first production build, so one runtime image serves everything and nothing ships on an unsupported runtime.
3. **There is no linux-arm64 RakNet library.** The restore output for `Ahwoo.Brutal.RakNet` 2026.10.2 (`src/Skein.Server.Host.Brutal/obj/project.assets.json`) lists native libraries for linux-x64, osx-arm64 and win-x64 only. Arm servers (Oracle Always Free A1, Hetzner CAX) cannot run the RakNet host unless the vendor adds an arm64 build, so stay on x86_64 plans. Prefer a Debian or Ubuntu based runtime image over Alpine until you have tested the native library there (my precaution; I did not test musl).
4. **Idle CPU is unknown.** I could not see how the BRUTAL job runner behaves at idle. If its worker threads spin, burstable or low-baseline shared CPUs (Lightsail 5 to 20% per vCPU, Fly shared 6.25%) will not hold, and "CPU per player" has to be measured after subtracting the idle load (section 13).

---

## 2. Cheap VPS providers for the starter box

All prices below were read on 2026-10-16. The "Basis" column says where each number came from.

### 2.1 Plans in the size range that matters (1 to 4 vCPU, 1 to 8 GB)

| Provider | Plan | vCPU | RAM | Storage | Traffic included per month | Monthly price | Basis and source |
|---|---|---|---|---|---|---|---|
| Hetzner (EU: FSN, NBG, HEL) | CX23, shared x86 | 2 | 4 GB | 40 GB NVMe | 20 TB | EUR 5.49 / $6.49, plus IPv4 EUR 0.50 / $0.60 | V: [price adjustment](https://docs.hetzner.com/general/infrastructure-and-availability/price-adjustment/) (effective 2026-06-15), [specs](https://www.hetzner.com/cloud/cost-optimized/). Page says currently unavailable. |
| Hetzner (EU) | CAX11, shared Arm64 | 2 | 4 GB | 40 GB | 20 TB | EUR 5.99 / $6.99 | V, same sources. Arm: this repo's RakNet library has no linux-arm64 build (section 1.6). Currently unavailable label. |
| Hetzner (EU) | CX33 | 4 | 8 GB | 80 GB | 20 TB | EUR 8.49 / $9.99 | V, same sources |
| Hetzner (EU) | CPX22, shared AMD | 2 | 4 GB | 80 GB | 20 TB | EUR 19.49 / $22.99 (was EUR 7.99) | V: [price adjustment](https://docs.hetzner.com/general/infrastructure-and-availability/price-adjustment/), [specs](https://www.hetzner.com/cloud/regular-performance/) |
| Hetzner (EU) | CPX02 and CPX12, shared AMD | 1 | 1 GB and 2 GB | 20 GB and 40 GB | 20 TB | U | Listed on the [Regular Performance page](https://www.hetzner.com/cloud/regular-performance/) but no price was shown and they are absent from the price-adjustment table |
| Hetzner (US: Ashburn, Hillsboro) | CPX11, shared AMD | 2 | 2 GB | 40 GB | 1 TB | EUR 17.49 / $20.49 (was EUR 5.99) | V, same sources. Overage per TB: see 2.3 |
| Hetzner (US) | CPX21 | 3 | 4 GB | 80 GB | 2 TB | EUR 31.99 / $37.49 | V, same sources |
| DigitalOcean | Basic | 1 | 1 GiB | 25 GiB | 1,000 GiB | $6.00 | V: [DO Droplet pricing](https://www.digitalocean.com/pricing/droplets) |
| DigitalOcean | Basic | 1 | 2 GiB | 50 GiB | 2,000 GiB | $12.00 | V, same page |
| DigitalOcean | Basic | 2 | 2 GiB | 60 GiB | 3,000 GiB | $18.00 | V, same page |
| DigitalOcean | Basic | 2 | 4 GiB | 80 GiB | 4,000 GiB | $24.00 | V, same page |
| Vultr | Regular `vc2-1c-1gb` | 1 | 1 GB | 25 GB | 1 TB | $5.00 | V: [Vultr plans API](https://api.vultr.com/v2/plans?type=vc2&per_page=25) (the marketing pricing page returned HTTP 403 to an automated fetch) |
| Vultr | Regular `vc2-1c-2gb` | 1 | 2 GB | 55 GB | 2 TB | $10.00 | V, same API |
| Vultr | Regular `vc2-2c-4gb` | 2 | 4 GB | 80 GB | 3 TB | $20.00 | V, same API |
| Vultr | High Frequency `vhf-2c-4gb` | 2 | 4 GB | 128 GB | 3 TB | $24.00 | V: [Vultr HF plans API](https://api.vultr.com/v2/plans?type=vhf&per_page=25) |
| Vultr | Regular `vc2-4c-8gb` | 4 | 8 GB | 160 GB | 4 TB | $40.00 | V, vc2 API |
| Akamai / Linode | Nanode 1 GB | 1 | 1 GB | 25 GB | 1 TB | $5.00 | V: [Akamai CPU pricing](https://www.akamai.com/cloud/pricing/cpu), [Linode types API](https://api.linode.com/v4/linode/types?page_size=100) |
| Akamai / Linode | Linode 2 GB | 1 | 2 GB | 50 GB | 2 TB | $12.00 | V, same |
| Akamai / Linode | Linode 4 GB | 2 | 4 GB | 80 GB | 4 TB | $24.00 | V, same |
| Akamai / Linode | Linode 8 GB | 4 | 8 GB | 160 GB | 5 TB | $48.00 | V, same |
| AWS Lightsail | $7 bundle (burstable, 10% baseline per vCPU) | 2 | 1 GB | 40 GB | 2 TB | $7.00 | V: [Lightsail pricing](https://aws.amazon.com/lightsail/pricing/), [baselines](https://docs.aws.amazon.com/lightsail/latest/userguide/baseline-cpu-performance.html) |
| AWS Lightsail | $12 bundle (20% baseline) | 2 | 2 GB | 60 GB | 3 TB | $12.00 | V, same |
| AWS Lightsail | $24 bundle (20% baseline) | 2 | 4 GB | 80 GB | 4 TB | $24.00 | V, same. Instances created on or before 2023-06-29 have 1 vCPU on the smaller bundles |
| Oracle Cloud Always Free | Ampere A1 Flex (Arm) | up to 2 OCPU in total | up to 12 GB in total | up to 200 GB boot plus block in total | 10 TB outbound | $0 | V: [Oracle Always Free resources](https://docs.oracle.com/en-us/iaas/Content/FreeTier/freetier_topic-Always_Free_Resources.htm). Catches below |
| OVHcloud (not on your list) | VPS-1 | 2 | 4 GB | 40 GB NVMe | unlimited, 500 Mbps | "from" $4.54 | S/U: [OVHcloud US VPS page](https://us.ovhcloud.com/vps/). A "from" price; the commitment term and US datacenter list are not stated on the page, so I do not rely on it. Anti-DDoS is advertised as included |


### 2.2 Locations (US sites matter for an action RPG)

| Provider | US locations | Canada | Europe | Source |
|---|---|---|---|---|
| Hetzner | Ashburn (VA), Hillsboro (OR). US plans are CPX (shared AMD) and CCX (dedicated) only | none | Falkenstein, Nuremberg, Helsinki. Plus Singapore | V: [Regular Performance page](https://www.hetzner.com/cloud/regular-performance/), [price adjustment](https://docs.hetzner.com/general/infrastructure-and-availability/price-adjustment/) |
| DigitalOcean | NYC1, NYC2, NYC3, SFO2, SFO3, ATL1, RIC1 (Richmond), MKC1 (Kansas City). ATL1, RIC1 and MKC1 only offer Premium CPU types and SFO2 lacks Premium AMD; Premium prices were not verified (U) | Toronto | Amsterdam, Frankfurt, London | V: [DO regional availability](https://docs.digitalocean.com/platform/regional-availability/) |
| Vultr | For the Regular plans: New Jersey, Atlanta, Chicago, Dallas, Miami, Seattle, Los Angeles, Silicon Valley (Honolulu exists but only on some Intel plans) | Toronto | Amsterdam, Frankfurt, London, Manchester, Madrid, Paris, Stockholm, Warsaw | V: [Vultr regions API](https://api.vultr.com/v2/regions?per_page=100), [plans API](https://api.vultr.com/v2/plans?type=vc2&per_page=25) |
| Akamai / Linode | Atlanta, Chicago, Dallas, Fremont, Los Angeles, Miami, Newark, Seattle, Washington DC (two sites) | Toronto | Amsterdam, Frankfurt (two), London (two), Madrid, Milan, Paris (two), Stockholm | V: [Linode regions API](https://api.linode.com/v4/regions?page_size=100) |
| AWS Lightsail | N. Virginia, Ohio, Oregon | Canada (Central) | Frankfurt, Ireland, London, Paris, Stockholm, Spain (opt-in) | V: [Lightsail regions](https://docs.aws.amazon.com/lightsail/latest/userguide/understanding-regions-and-availability-zones-in-amazon-lightsail.html) |
| Oracle Always Free | Any OCI region can be your home region, but Always Free compute must be created in the home region you picked at sign-up | - | - | V: Oracle doc linked in 2.1 |
| OVHcloud | U | U | U | Not stated on the pages I could read |

### 2.3 Extras that change the real bill

| Provider | Traffic overage | Public IPv4 | Billing and CPU model |
|---|---|---|---|
| Hetzner | About EUR 1.00 per TB, roughly $1.20/TB (S: [egresscost](https://egresscost.com/hetzner), data read 2026-09-30; Hetzner's own page did not show the number). Only outgoing traffic is billed; alerts at 75% and 100% of the allowance but no cap (V: [billing FAQ](https://docs.hetzner.com/cloud/billing/faq/)). Whether the allowance is pooled across servers is not documented on the pages I read | EUR 0.50 / $0.60 per month per primary IPv4; IPv6 free (V: [Primary IPs](https://docs.hetzner.com/cloud/servers/primary-ips/overview/)) | Hourly, capped at the monthly price (V: billing FAQ). Shared vCPU |
| DigitalOcean | $0.01 per GiB, transfer pooled at team level (V: [Droplet pricing doc](https://docs.digitalocean.com/products/droplets/details/pricing/), last verified 25 Aug 2026) | Not itemised for bundled plans in the doc I read. For newer "v5" configurations IPv4 is a separate line item at an introductory $0.00 that the doc says is subject to change (V, same doc) | Hourly. Shared CPU is not guaranteed: neighbours can leave you fractions of a hyper-thread (V: [choosing a plan](https://docs.digitalocean.com/products/droplets/concepts/choosing-a-plan/)) |
| Vultr | $0.01 per GB; account-level pooled allowances plus 2 TB free outbound per month (V: [overage doc](https://docs.vultr.com/support/platform/billing/what-is-the-bandwidth-overage-rate), updated 2025-12-16; [2022 announcement](https://blogs.vultr.com/Vultr-Announces-Reduced-Bandwidth-Pricing-2-Tb-Of-Free-Monthly-Egress-Free-Ingress-And-Global-Pooling)) | Included from the $3.50 plan up; an IPv6-only $2.50 plan exists (V: plans API) | Hourly rates published. Shared (Regular, High Frequency); dedicated-vCPU Optimized plans exist (section 9) |
| Akamai / Linode | $0.005 per GB (V: Akamai CPU pricing page) | Not itemised on the pages I read | Hourly and monthly. Shared; dedicated plans exist |
| AWS Lightsail | $0.09 per GB appears in AWS's own worked example for US East (Ohio); no standalone overage table was shown (V, example only) | Dual-stack bundles cost $1.50 (smallest) to $4 (4 GB and up) more than the matching IPv6-only bundle (V) | Monthly bundles. Burstable: baseline per vCPU is 5% ($5), 10% ($7), 20% ($12, $24), 30% ($44); sustained use above baseline spends and then exhausts burst capacity (V: baselines doc) |
| Oracle | 10 TB per month outbound free; price beyond that is U | One public IP per instance | Free. Arm A1 |

### 2.4 Notes per provider

**Hetzner Cloud.**

- Price adjustment effective 2026-06-15 (page last changed 2026-07-08): CX23 EUR 3.99 to 5.49, CAX11 4.49 to 5.99, CPX22 7.99 to 19.49, US CPX11 5.99 to 17.49 (all net, excluding IPv4). One secondary source calls this the fourth Hetzner pricing action of 2026 ([PrivateDevOps](https://privatedevops.com/news/hetzner-june-2026-cloud-price-increase-what-to-do), S); I could confirm only the June one on Hetzner's own page.
- Capacity: see section 1.1. Hetzner's FAQ says restrictions can block creating, rescaling and moving servers and are lifted automatically; try again the following day ([FAQ](https://docs.hetzner.com/cloud/general/faq), V).
- Existing servers keep their old price until you rescale them, which re-prices them (V: price-adjustment page and billing FAQ).
- Included US traffic is 1 TB on CPX11 and rises with plan size (2 TB CPX21, 3 TB CPX31); EU is 20 TB; Singapore is 0.5 to 5 TB (V: Regular Performance page).
- DDoS: Hetzner says its protection is always on and free for all customers, with traffic that matches known attack patterns routed through scrubbing filters ([Hetzner DDoS page](https://www.hetzner.com/unternehmen/ddos-schutz/), V).
- Secondary analysis of the capacity situation: [Findstack, 2026-09-28](https://findstack.com/resources/is-hetzner-reliable) and [Findstack, 2026-09-16](https://findstack.com/resources/hetzner-price-increase-2026) (S).

**DigitalOcean.** Bundled-plan Droplets are capped at 672 billed hours a month (28 days), so a 30-day month does not cost more than the listed price (V: Droplet pricing doc). DDoS protection is free and always on; if an attack exceeds mitigation capacity, DigitalOcean blackholes all inbound traffic to the target IP and emails the account owner ([DDoS docs](https://docs.digitalocean.com/products/networking/ddos/), V).

**Vultr.** DDoS protection adds 10 Gbps of mitigation capacity per Cloud Compute or High Frequency instance and usually reroutes within 60 seconds. The page says higher tiers will come later as a paid add-on and does not say whether the base level is free ([Vultr DDoS docs](https://docs.vultr.com/ddos-protection), V).

**Akamai / Linode.** Eleven regions in the US and Canada, hourly billing, the lowest overage rate of the group. Dedicated-CPU G8 plans are billed hourly with no bundled transfer (V: Akamai CPU pricing page).

**AWS Lightsail.** Strong network and region footprint, but the burstable CPU model is the wrong fit once the tick loop sits near the baseline for hours. AWS also charges $0.005 per hour (about $3.65 a month) for each in-use public IPv4 address on VPC resources ([AWS VPC pricing](https://aws.amazon.com/vpc/pricing/), V).

**Oracle Cloud Always Free: the catches.**

1. The Arm A1 allowance was halved from 4 OCPU / 24 GB to 2 OCPU / 12 GB in mid-2026 without an announcement; instances over the new limit were stopped on free-only accounts ([Oracle doc](https://docs.oracle.com/en-us/iaas/Content/FreeTier/freetier_topic-Always_Free_Resources.htm), V; [InfoQ, 2026-07-03](https://infoq.com/news/2026/07/oracle-cloud-free-tier-limits/), S). Reports that Pay-As-You-Go accounts keep the old allowance are not confirmed by Oracle.
2. Idle reclamation: Oracle may reclaim an Always Free instance if, over a 7-day period, CPU p95 is under 20%, network use is under 20% and (A1 only) memory use is under 20%. A 4-player game server will usually satisfy all three (V: Oracle doc).
3. "Out of host capacity" errors are common enough that Oracle documents them. The documented fixes are to retry, change availability domain, or upgrade to Pay As You Go (V).
4. Always Free resources must be created in the home region chosen at sign-up (V).
5. It is an Arm machine. In this repository the RakNet package (`Ahwoo.Brutal.RakNet` 2026.10.2) ships native libraries for linux-x64, osx-arm64 and win-x64 only (section 1.6), so the RakNet host cannot run on it unless an arm64 build appears. It could still host a pure-.NET TCP side service.

## 3. Platform-as-a-service: does a UDP game server fit?

Short answer: only Fly.io can accept public UDP, and it costs more than a plain VPS for a steady tick loop. Railway and Render cannot host the RakNet port at all. Prices and documentation in this section were read on 2026-10-16.

| Platform | Public inbound UDP | What the documentation says (V unless marked) | Price relevant to Skein | Verdict |
|---|---|---|---|---|
| Fly.io | Yes, with conditions | UDP needs a dedicated IPv4 address; shared IPv4 and public IPv6 cannot carry it. The server must bind its UDP socket to the special `fly-global-services` address and reply from it, because binding `0.0.0.0` makes Linux answer from the wrong source address. Fly rewrites the IP but not the port, so the internal and external UDP ports must match. Plan for about 1300-byte packets, not 1500, because of WireGuard and proxy overhead ([UDP and TCP docs](https://docs.fly.io/networking/udp-and-tcp)). Shared vCPUs get a 6.25% baseline with a burst balance capped at 500 seconds; when it is empty the vCPU is throttled to the baseline. Performance vCPUs get 100% ([CPU performance](https://docs.fly.io/machines/cpu-performance)) | `shared-cpu-1x` 1 GB is $6.70/month in Ashburn, but a 20 Hz tick loop will exhaust the burst balance. `performance-1x` 2 GB is $33.00/month. Dedicated IPv4 $2/month. Volumes $0.15/GB-month. Egress $0.02/GB in North America and Europe, $0.04/GB in Asia Pacific and South America. No free tier for new organisations ([pricing](https://docs.fly.io/about/pricing)). Managed Postgres starts at $38/month plus $0.28/GB ([Fly Postgres docs](https://docs.fly.io/postgres/)) | Works technically. About $36.50/month for one full vCPU, 2 GB and 10 GB of volume (my sum of the quoted rates), versus $20 for 2 vCPU / 4 GB at Vultr. Revisit for a later edge or short-lived-instance role |
| Railway | No | TCP Proxy is the only way to expose non-HTTP services, and its docs describe raw TCP only ([TCP proxy](https://docs.railway.com/networking/tcp-proxy)). A feature request for inbound UDP, opened 2026-01-17, was still marked as under review with user replies as late as 2026-07-21 ([Railway Station thread](https://station.railway.com/feedback/udp-inboud-connections-9f036ffc)). A search summary of Railway staff replies says inbound UDP is unsupported while outbound UDP works (S) | Hobby $5/month with $5 of usage; usage rates $20 per vCPU-month, $10 per GB-month of RAM, $0.05/GB egress, $0.15/GB-month volumes ([plans](https://docs.railway.com/reference/pricing/plans)). One vCPU plus 1 GB is about $30/month by those rates | Does not fit. Could host an HTTP-only side service |
| Render | No | Public traffic reaches a web service over HTTP/HTTPS only, forwarded to one HTTP port per service ([web services](https://render.com/docs/web-services)). Private services accept any protocol on almost any port but are not reachable from the public internet ([private services](https://render.com/docs/private-services)). I found no Render page offering a public UDP or raw TCP listener | U: Render's pricing page returned no plan tables to the fetch tool. Third-party July 2026 tables list $7, $25 and $85 instance tiers, but Render changed its plans in 2026, so I assert no number | Does not fit |

Other PaaS products (container-app services from the big clouds, Heroku-style hosts) were not evaluated; confirm public UDP support on the provider's own documentation before considering any of them.


## 4. Hosting at home (spare PC)

### 4.1 Options for reaching a home server

Prices and documentation in this subsection were read on 2026-10-16.

| Approach | Carries the UDP game port? | Cost | Verdict |
|---|---|---|---|
| Tailscale (every player joins your tailnet) | Yes. It is a WireGuard-based overlay, so any UDP between tailnet addresses works. Direct paths give the lowest latency and highest throughput; when NAT traversal fails, traffic goes through DERP relays, which Tailscale says are generally slower with lower maximum throughput ([connection types](https://tailscale.com/kb/1257/connection-types), V). The tunnel MTU is 1280 and packets larger than that can be dropped silently ([Tailscale troubleshooting](https://tailscale.com/docs/reference/troubleshooting/network-configuration/tcp-connection-two-devices), V) | Personal plan: free for 6 users in one tailnet, with unlimited devices per user ([KB, last validated 2026-04-08](https://tailscale.com/kb/1154/free-plans-discounts); [pricing](https://tailscale.com/pricing)). The paid Standard plan is listed at $8 per user per month (V, pricing page) | Best home option for a trusted group of up to 6. Cap the RakNet MTU near 1200 bytes so datagrams are not dropped or fragmented inside the tunnel |
| Cloudflare Tunnel with a public hostname | No. Published-application service types are HTTP, HTTPS, TCP, SSH, RDP, SMB and similar; the non-HTTP types are carried over WebSocket and need `cloudflared` on the client; UDP is not on the list ([protocols](https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/routing-to-tunnel/protocols/), V) | Plan terms not re-verified | Use it for the HTTP admin port or a Grafana dashboard, never for RakNet |
| Cloudflare proxy (orange cloud) or Spectrum | No for arbitrary ports. The proxy handles a fixed list of HTTP/HTTPS ports (for example 80, 8080, 443, 8443). Spectrum for all TCP and UDP ports is Enterprise only ([network ports](https://developers.cloudflare.com/fundamentals/reference/network-ports/), [Spectrum](https://developers.cloudflare.com/spectrum/), V) | Enterprise, quote-based | Keep the game DNS record DNS-only (grey cloud) |
| Cloudflare WARP private-network routes | UDP support for private-network routes was announced in 2021 (early access then); WARP is the client side of that design, so each player would run WARP enrolled in your Zero Trust organisation (my inference) ([Cloudflare blog, 2021-12-08](https://blog.cloudflare.com/extending-cloudflares-zero-trust-platform-to-support-udp-and-internal-dns/), S for current status) | U | A heavier alternative to Tailscale; I did not verify current plan limits |
| Router port forwarding plus dynamic DNS | Yes | $0. DuckDNS describes itself as free dynamic DNS ([DuckDNS](https://www.duckdns.org/), V) | Acceptable for a short, small public test only. Risks below |

### 4.2 Port forwarding risks

1. **Exposure.** Your home IP and every listening Skein port (RakNet parsing, TCP, any HTTP admin) are reachable from the whole internet from a machine on your home network.
2. **No DDoS shield.** VPS providers filter volumetric floods (Hetzner, DigitalOcean, Vultr and OVHcloud all advertise it, section 2); your household link does not. A flood knocks everyone in the house offline.
3. **CGNAT.** If the router's WAN address is inside 100.64.0.0/10 (the shared address space reserved for carrier-grade NAT, [RFC 6598](https://www.rfc-editor.org/rfc/rfc6598)), inbound port forwarding cannot work. Ask the ISP for a public IPv4 address or use Tailscale.
4. **Dynamic address.** Run a DDNS updater and keep the TTL low. With Cloudflare DNS the record must be DNS-only.
5. **UPnP and flat networks.** Forward statically, disable UPnP, and put the server on its own VLAN or a dedicated machine so a game bug cannot reach the family PCs.
6. **ISP terms and reliability.** Some residential terms of service forbid servers (check yours). Power cuts, ISP outages and disk failure are your problem, so the nightly off-site backup (section 8) is mandatory.
7. **Windows host quirks.** If the spare PC runs Windows with Docker Desktop or WSL2, UDP port publishing typically passes through an additional virtual NAT layer (WSL2 uses a NAT network by default; I did not test this). Prefer a native Ubuntu Server install on the spare PC and follow section 11.

### 4.3 When home hosting is the right call, and when it stops being

Home hosting is the right call for the first weeks when **all** of these hold:

- Every player is someone you trust, there are at most 6 of them (the Tailscale free-plan user limit), and all can install the Tailscale client.
- The host is wired (not Wi-Fi), always on, and reachable at about 50 ms RTT or less from each player over the tailnet (`tailscale ping` shows whether the path is direct or relayed).
- You run the same Docker Compose stack you will later run on a VPS, and the nightly off-site backup from section 8 is already working.
- You accept that an ISP or power outage means no game that day.

Cost is mostly electricity. A 40 W idle machine uses about 29 kWh a month (40 W x 730 h), so at an example tariff of $0.20 per kWh that is about $5.84 a month. A $20 VPS is not much more expensive, so the real reason to host at home is control and a free development loop, not savings.

It stops being the right call at the first of these:

- **The first player you do not personally know.** Public exposure, DDoS, and your ISP terms all become your problem.
- **More than 6 people** (free plan limit), or players far enough away that residential routing pushes RTT past about 80 ms.
- **Upload bandwidth.** At the section 9 assumption of 6 KB/s per player downstream, 100 players need about 4.8 Mbit/s of sustained upload and 500 players about 24 Mbit/s, which exceeds many residential uplinks. Realistic ceiling: 100 to 200 concurrent players even with a good ISP.
- **You need better than "usually up".** A single consumer machine with no redundancy is not a service.

When you leave home hosting, the move is: `docker compose down`, restore the latest dump on the VPS, change the DNS record (keep the TTL at 60 to 300 seconds while you are still deciding), and tell players to reconnect.

## 5. PostgreSQL: managed versus self-hosted on the same VPS

Prices and limits in this section were read on 2026-10-16.

| Option | Price | Limits that matter for a game server | Basis | Fit |
|---|---|---|---|---|
| Self-hosted `postgres` container on the game VPS | $0 extra; roughly 0.3 to 1 GB of RAM at this scale (my estimate) | You own backups, upgrades and tuning. Loopback-only port. Shares CPU and disk with the game | Compose file in section 11 | Best for stages 1 and 2: no network hop, no second bill, same latency as a local call |
| Neon Free | $0 | 1 GB storage per project; 100 CU-hours per project per month, which is 400 hours at the smallest 0.25 CU compute and less than the 730 hours in a month, so it cannot stay awake all month; compute suspends after 5 minutes idle and this cannot be disabled; 5 GB egress; 6-hour history window ([pricing](https://neon.com/pricing), [plans](https://neon.com/docs/introduction/plans)) | V | Development or staging only |
| Neon Launch | Usage-based, no monthly minimum: $0.106 per CU-hour, $0.35 per GB-month, 500 GB egress included then $0.10/GB, instant restore $0.20 per GB-month. A 0.25 CU compute running all month is about $19.34 plus storage (my arithmetic: 0.25 x 730 x $0.106), for example $1.75 for 5 GB | History window up to 7 days; scale-to-zero can be disabled | V: pricing and plans pages above | Reasonable managed option from stage 2 or 3. Create the project in the same cloud region as the game hosts or every save pays a WAN round trip |
| Supabase Free | $0 | 500 MB database, shared CPU, 2 active projects, projects paused after 1 week of inactivity, no automatic backups ([pricing](https://supabase.com/pricing)) | V | Not suitable for a live game |
| Supabase Pro | From $25/month, which includes a $10 compute credit; Micro compute $10, Small $15, Medium $60 | 8 GB disk then $0.125/GB; daily backups kept 7 days; point-in-time recovery is an add-on at $100/month per 7 days of retention | V | Expensive for what you need early |
| DigitalOcean Managed PostgreSQL | Single node from $15.00/month (1 GiB RAM, 1 vCPU) per the docs, $15.15 on the marketing page; 2 GiB / 1 vCPU is $30.45 on the marketing page; HA starts at $30 plus a matching standby ([docs pricing](https://docs.digitalocean.com/products/databases/postgresql/details/pricing/), last verified 2026-09-30; [marketing page](https://www.digitalocean.com/pricing/managed-databases)) | 22 backend connections on the 1 GiB plan, 47 on 2 GiB, 97 on 4 GiB; point-in-time recovery limited to 7 days; restores go to a new node. From 2026-10-15 for new accounts (2026-11-30 for all accounts) new Standard Edition clusters cannot use plans above 4 GiB RAM or add standby or read-only nodes ([limits doc](https://docs.digitalocean.com/products/databases/postgresql/details/limits/)). The Advanced Edition has no Basic shared-CPU plans; I did not find its prices (U) | V | Good upgrade path if the game already runs on DO. Check the edition rules before designing HA |
| Fly Managed Postgres | Basic $38/month (shared-2x CPU, 1 GB) plus $0.28/GB-month; HA, backups and pooling included ([Fly docs](https://docs.fly.io/postgres/)) | Only sensible next to Fly compute | V | Not recommended |
| Hetzner | No Hetzner-managed PostgreSQL product turned up; a third-party guide says users run Postgres themselves on a cloud server (S: [Sliplane](https://frontend.sliplane.io/blog/5-cheap-ways-to-host-postgres)). I did not check Hetzner's full product list | n/a | S/U | Self-host in Docker |

Recommendation:

1. **Stages 1 and 2: self-host in Docker on the same VPS**, with off-provider dumps (section 8). Add WAL archiving for point-in-time recovery at stage 2.
2. **Stage 3: move Postgres to its own host** (still self-hosted) or to a managed service in the same region if you would rather pay than operate it. Do not choose a managed service whose region differs from the game hosts.
3. **SQLite or JSON files for the first weeks are fine** provided the persistence code sits behind an interface and you back up correctly: with SQLite use WAL mode and `sqlite3 game.db ".backup out.db"`, never a plain copy of a live file; with JSON write to a temp file and rename. Plan the move to Postgres before the first public playtest, because migrations from ad-hoc JSON get harder with every player.
4. **Image tag matters.** Current official images are PostgreSQL 18 (latest), 17, 16, 15 and 14, with 19rc1 in release-candidate ([Docker Hub](https://hub.docker.com/_/postgres), V). Start new projects on 18 and use the 18-style volume path shown in section 11, or you lose data when the container is re-created.

## 6. Orchestration later: Kubernetes with Agones versus plain VMs plus a small control plane

### 6.1 What each option is

- **Agones** is a library for hosting, running and scaling dedicated game servers on Kubernetes. Its main objects are GameServer, Fleet, FleetAutoscaler and GameServerAllocation. The current release listed on GitHub is v1.61.0 (dated 24 Sep, year not shown; the previous two were 12 Aug and 01 Jul), and it supports Kubernetes 1.34, 1.35 and 1.36 ([Agones docs](https://agones.dev/site/docs/), [releases](https://github.com/googleforgames/agones/releases), [installation](https://agones.dev/site/docs/installation/)). Each game server must integrate the Agones SDK; the SDK list includes C#, and the three calls that matter are Ready(), Health() and Shutdown() ([SDKs](https://agones.dev/site/docs/guides/client-sdks/)). Ports use a policy (Dynamic, Static, Passthrough or None) that maps a hostPort on the node to the container, and firewall access must be open for the port range the game servers use ([GameServer reference](https://agones.dev/site/docs/reference/gameserver/), installation page).
- **Plain VMs plus a small control plane** means Docker Compose or systemd on each VM, plus a small service of your own (a "directory") that knows which host and UDP port runs which zone. Hosts register and send heartbeats; the TCP gateway tells a client which `host:port` to use for the zone it is entering.

### 6.2 Side by side

| Question | VMs + systemd/Compose + control plane | Kubernetes + Agones |
|---|---|---|
| What you build | A host agent that starts and stops zone containers via the Docker API, a directory table in Postgres, deploy scripts. A few hundred lines of C# plus an Ansible or cloud-init file | Agones SDK integration in the server, Fleet and allocation manifests, cluster setup and upgrades |
| Fixed platform cost | $0 | Control plane: DigitalOcean Kubernetes is free, with an optional HA control plane at $40/month ([DOKS pricing](https://www.digitalocean.com/pricing/kubernetes), V); AWS EKS is $0.10 per cluster-hour, about $73/month, on standard support ([EKS pricing](https://aws.amazon.com/eks/pricing/), V). I did not verify GKE or the Hetzner and Vultr options. Worker nodes cost the same as plain VMs |
| Skills and ops load | Docker, systemd, SSH | Kubernetes upgrades, networking, node pools; the current Agones release supports three Kubernetes minor versions (1.34 to 1.36), so expect to upgrade the cluster regularly |
| Persistent world zones (long-lived, sticky state) | Natural: a zone is a row in the directory and a container on a host | Possible, but fleets, allocation and autoscaling are designed around servers that are allocated, played and shut down, so you use less of what Agones is good at |
| Instanced content (dungeons, arenas, short-lived) | You write allocation and cleanup yourself | The strongest fit |
| Networking | One public IP per VM, one UDP port per zone process | hostPort per GameServer on nodes that have public IPs, plus a firewall rule for the port range |
| Autoscaling | Scripts against the provider API, or manual | FleetAutoscaler, plus a cluster autoscaler for nodes |

### 6.3 When the jump pays off

My judgement, not a measured threshold. Stay on VMs and a small control plane until at least two of these are true:

1. You run more than about 10 to 15 game hosts, or more than about 100 zone or instance processes at peak.
2. You must add and remove capacity within minutes (launches, events) in more than one region, and manual or scripted scaling is not keeping up.
3. A large share of your load is instanced, short-lived content with hundreds of allocations an hour.
4. Someone on the team already runs Kubernetes in production and wants to own it.

Until then the control plane is cheaper than learning Agones: about 5000 CCU needs roughly 16 hosts (section 9), which a directory table and an Ansible playbook handle comfortably. If instanced content arrives early, a hybrid is reasonable: keep the persistent world on VMs and run only the instances on Agones.

## 7. Observability on a budget

### 7.1 Options

Prices and limits in this subsection were read on 2026-10-16.

| Option | Cost | Limits | Basis |
|---|---|---|---|
| Grafana Cloud Free | $0, permanent | 10k active metric series, 50 GB logs, 50 GB traces and 50 GB profiles ingested per month, 14-day retention, 3 active users ([Grafana pricing](https://grafana.com/pricing/)) | V |
| Grafana Cloud Pro | $19/month platform fee including the free-tier volumes; extra metrics from $6.50 per 1k series; logs billed as $0.05/GB processing + $0.40/GB write + $0.10/GB retention (same page) | Volume discounts apply automatically at scale | V |
| Self-hosted Prometheus + Grafana + Loki in Docker | $0 for software. Needs RAM and disk of its own; I estimate about 1 GB of RAM for all three at this scale (my estimate, not measured) | You maintain upgrades, retention and access control | S/U for the RAM figure |

Sizing rule from Prometheus documentation: disk = retention seconds x samples per second x bytes per sample, with an average of 1 to 2 bytes per sample and a default retention of 15 days ([Prometheus storage](https://prometheus.io/docs/prometheus/latest/storage/), V). Worked example, my arithmetic: 10,000 series scraped every 15 seconds is 667 samples per second, which is about 86 to 115 MB per day, or 1.3 to 1.7 GB for 15 days. Loki indexes only labels, not log text, and can run as a single binary ([Loki overview](https://grafana.com/docs/loki/latest/get-started/overview/), V).

### 7.2 Recommendation

1. **Stages 1 and 2: Grafana Cloud Free.** Run Grafana's collector (Alloy) on the game box to push metrics and logs off-box. Keep series under 10k: the .NET runtime metrics, a few dozen custom game metrics, node metrics and the Postgres exporter fit comfortably if you avoid per-player labels. Monitoring survives the game box dying, which is exactly when you need it.
2. **Stage 3 onward: a separate small VPS** (2 vCPU / 4 GB, about $20) running Prometheus, Loki and Grafana, once series or log volume exceed the free tier or you want longer retention. Reach Grafana over Tailscale or an SSH tunnel, or publish it through Cloudflare Tunnel with access control; do not expose it on a public port.
3. **A dead-man's switch for backups:** healthchecks.io, free plan 20 jobs with 100 log entries per job ([pricing](https://healthchecks.io/pricing/), V).

### 7.3 What to measure for a tick-based server

| Metric | Why | Alert when |
|---|---|---|
| Tick duration p50, p95, p99 per zone process | The budget is 50 ms at 20 Hz and 33.3 ms at 30 Hz | p99 above 70% of the budget for 5 minutes |
| Tick overruns per minute | Missed ticks are felt as rubber-banding | Any sustained increase |
| Players per zone and per host | Capacity planning and the section 9 assumptions | Above your per-vCPU planning figure |
| Bytes and packets in and out per second, per player | Validates the bandwidth assumption and the egress bill | More than 2x the planning value |
| .NET GC pause time and allocation rate | Pauses show up as tick spikes | Gen2 pauses near the tick budget |
| Linux UDP receive buffer errors (`RcvbufErrors` in `/proc/net/snmp`) | Dropped inbound datagrams look like network loss | Any increase |
| Postgres save latency and queue depth | Persistence stalls back-pressure the simulation | p99 save above 1 second |
| Node CPU steal time | Evidence of noisy neighbours on shared vCPUs | Sustained steal above a few percent |
| Disk free, backup age | Basic hygiene | Disk above 80%, last good backup older than 26 hours |

## 8. Backups: pg_dump to object storage

### 8.1 Price per GB

Prices in this subsection were read on 2026-10-16.

| Service | Storage | Egress and operations | Free allowance | Basis |
|---|---|---|---|---|
| Cloudflare R2 (Standard) | $0.015 per GB-month | Egress free. Class A $4.50 per million, Class B $0.36 per million | 10 GB-month storage, 1M Class A, 10M Class B each month (Standard storage only) ([R2 pricing](https://developers.cloudflare.com/r2/pricing/)) | V |
| Backblaze B2 | $6.95 per TB per month (about $0.00695 per GB) | Free egress up to 3x your average monthly stored data, then $0.01/GB; API-call fees are negligible for one upload a night (see the page for the call classes) | First 10 GB free ([B2 pricing](https://www.backblaze.com/cloud-storage/pricing)); the page shows no effective date | V |
| Hetzner Object Storage | U. Hetzner's page rendered without numbers. A July 2026 comparison reports EUR 6.49 per month net for a base that includes 1 TB storage and 1 TB egress, EUR 8.70 per extra TB-month and EUR 1 per extra TB of egress ([Sliplane](https://sliplane.io/blog/hetzner-object-storage-alternatives)). Other sites quote EUR 4.99 or EUR 5.99 for the base | Ingress and S3 requests free; billing is hourly with a monthly cap; locations FSN1, NBG1, HEL1 ([Hetzner docs](https://docs.hetzner.com/storage/object-storage/overview/)) | none | S/U |

Monthly cost for typical backup volumes (my arithmetic from the verified R2 and B2 rates; the stored sizes are assumptions):

| Stage | Stored backups | R2 | B2 |
|---|---|---|---|
| 1 | about 15 GB (30 daily dumps of roughly 0.5 GB) | about $0.08 | about $0.04 |
| 3 | about 200 GB (dumps plus WAL archive) | about $2.85 | about $1.32 |
| 4 | about 1.2 TB (dumps plus WAL archive) | about $17.85 | about $8.27 |

At every stage the backup bill is rounding error next to the compute bill, so choose by safety: **store backups at a different provider and account than the game server**, so one billing problem or one compromised credential does not take both. R2 has zero egress fees, which makes the restore drill free; B2 is cheaper per GB and gives free egress up to 3x stored data.

### 8.2 Strategy

- **Stage 1: nightly `pg_dump -Fc`** (custom format, already compressed) from the container, verify with `pg_restore --list`, upload with rclone, keep 3 days locally and 30 days remotely using the bucket's lifecycle rule. Recovery point objective: up to 24 hours. The script is in section 11, step 13.
- **Stage 2: add WAL archiving** with pgBackRest or WAL-G to the same bucket for point-in-time recovery, keep nightly dumps as the simple fallback. Recovery point objective: minutes.
- **Encrypt before upload** (age or rclone crypt). Dumps contain player data and account identifiers.
- **Restore drill monthly.** A backup you have never restored is a hope, not a backup. Time the drill; that time is your real recovery time objective. For a rebuilt VPS the sum is bootstrap (about 30 minutes) plus restore (minutes for a few GB).
- **Dead-man's switch.** The backup job pings healthchecks.io on success and the check alerts when no ping arrives within 26 hours.
- **Keep non-database state too:** the Compose file, `.env` (encrypted), the bootstrap script and any server config belong in the repository or in the encrypted backup.

## 9. Scaling path: four stages from 4 to 5,000 concurrent players

### 9.1 Assumptions (all are planning figures, not measurements)

Replace A2 and A7 with numbers from your own load test (section 13) as soon as you have them; every cost below scales almost linearly with them.

| ID | Assumption | Value used | Reasoning |
|---|---|---|---|
| A1 | Players per zone process (soft cap) | 150 | Beyond this, split the zone or instance it |
| A2 | CPU of one zone process at 150 players, 20 Hz, on a shared vCPU | 1.0 vCPU at peak | Deliberately pessimistic for a C# authoritative ARPG with monster AI and per-client snapshots |
| A3 | Planning ceiling per vCPU | 60% utilisation | Headroom for GC pauses, AI spikes and noisy neighbours |
| A4 | Result: players per vCPU | 90 (150 x 0.6) | Used for all sizing below |
| A5 | Per-host overhead | +15% vCPU and 1 GB RAM | OS, Docker, monitoring agent |
| A6 | RAM per zone process | 1 GB | Pessimistic; measure it |
| A7 | Downstream bandwidth per player at 20 Hz | 6 KB/s (48 kbit/s) | About 50 bytes of IP, UDP and RakNet headers per packet x 20 packets/s = 1 KB/s, plus roughly 250 bytes of delta-compressed snapshot per tick = 5 KB/s. At 30 Hz multiply by 1.5 |
| A8 | Upstream bandwidth per player | 1.5 KB/s | About 25 bytes of input plus 50 bytes of headers, 20 times a second. Ingress is free at the providers compared |
| A9 | Average concurrency | 40% of the peak CCU | Evening peaks; use 70% if your players are spread across time zones |
| A10 | Database writes | one save per player per 60 s plus event writes | About 83 writes/s at 5,000 CCU: modest for a 4 vCPU / 32 GB Postgres (assumption, not benchmarked) |
| A11 | Regions | 1 | All figures are per region |

Derived figures:

- vCPU needed = peak CCU / 90 x 1.15. That is 0.05 at 4 CCU (the box is sized by RAM and slack, not CPU), 0.6 at 50, 6.4 at 500 and 63.9 at 5,000.
- Egress per always-on player = 6,000 B/s x 2,592,000 s = 15.55 GB a month. Per peak CCU at 40% average concurrency that is 6.2 GB a month: **25 GB at 4 CCU, 311 GB at 50, 3.1 TB at 500, 31 TB at 5,000.**

### 9.2 The four stages

| | Stage 1 | Stage 2 | Stage 3 | Stage 4 |
|---|---|---|---|---|
| Peak concurrent players | 4 | 50 | 500 | 5,000 |
| Zone processes (A1) | 1 process, all zones | 1 to 2 | about 4 to 6 | about 35 to 40 |
| Shape | One VPS: game container + Postgres container, provider firewall, nightly dump off-provider | One larger VPS plus a staging VPS. Zones split into separate containers on the host (one UDP port each). WAL archiving for point-in-time recovery | 3 game hosts, a separate Postgres host, a control-plane/gateway host (directory, login, admin), a monitoring host, a staging host | 16 game hosts in one region, Postgres primary plus replica, 3 gateway/control hosts, monitoring, staging and CI. Optionally Agones for instanced content |
| What changes in the code and ops | Nothing special | Zone-to-port mapping, rate limits on handshake packets, reconnect logic in the client | The directory service: hosts register and heartbeat, the TCP gateway returns `host:port` for a zone, drain and restart per host | Automation for everything: cloud-init or Ansible, per-host rollouts, alerting, game-day tests |
| Exit trigger to the next stage | Sustained 15+ players, tick p99 above 60% of budget, or any public alpha | CPU above 50% at peak on the host, RAM above 70%, or you can no longer afford a few minutes of restart downtime | One zone process above its A1 cap, a single host above 60% CPU at peak, or DB CPU above 50% | Not applicable: add regions or revisit the architecture |

Topology sketch (text only):

```text
Stage 1 (4 CCU)

 players --UDP 27500 / TCP 27501--> provider firewall --> VPS (Ubuntu LTS, Docker, ufw)
                                                           +-- game container     (all zones, one process)
                                                           +-- postgres container (127.0.0.1:5432 only)
                                                           +-- nightly pg_dump --> object storage at ANOTHER provider
 metrics and logs ----------------------------------------> Grafana Cloud Free

Stages 3 and 4 (500 to 5,000 CCU, one region)

 players --TCP--> gateway/login hosts --reads--> directory (Postgres): "zone Z is at host:udp_port"
 players --UDP--> game host 1..N (several zone processes each, one UDP port per process)
                    +-- load/save --> Postgres primary --> replica
                    +-- heartbeat --> directory
 monitoring host (Prometheus, Loki, Grafana)   staging host   backups --> object storage at another provider
```

### 9.3 Ladder A: Vultr list prices, itemised

Prices from the Vultr plan APIs read on 2026-10-16: [Regular](https://api.vultr.com/v2/plans?type=vc2&per_page=25) and [Optimized Cloud Compute](https://api.vultr.com/v2/plans?type=voc&per_page=25). The Optimized plans have dedicated vCPUs; the listing I read showed 25 of 51 plans, which is enough for the rows used here. Backup costs use the R2 rate from section 8.

| Stage | Line items | Monthly |
|---|---|---|
| 1 (4 CCU) | 1 x `vc2-2c-4gb` $20; backups about $0.08; Grafana Cloud Free $0 | **about $20** |
| 2 (50 CCU) | 1 x `vc2-4c-8gb` $40 (production, headroom for events); 1 x `vc2-1c-2gb` $10 (staging); backups plus WAL archive about $1 (65 GB) | **about $51** (or about $31 if you stay on the $20 plan) |
| 3 (500 CCU) | 3 x `vc2-4c-8gb` $120 (game, N+1); 1 x `vc2-4c-8gb` $40 (Postgres); 1 x `vc2-2c-4gb` $20 (control and gateway); 1 x `vc2-2c-4gb` $20 (monitoring); 1 x `vc2-2c-4gb` $20 (staging); backups about $3 | **about $223** |
| 4 (5,000 CCU) | 16 x `vc2-4c-8gb` $640 (game); 2 x `voc-m-4c-32gb-200s-amd` $320 (Postgres primary and replica, dedicated vCPU, 32 GB); 3 x `vc2-2c-4gb` $60 (gateways and control); 1 x `vc2-4c-8gb` $40 (monitoring); 2 x `vc2-4c-8gb` $80 (staging and CI); backups about $18 | **about $1,158** |
| 4, dedicated vCPUs on game hosts | Replace the 16 game hosts with 16 x `voc-c-4c-8gb-75s-amd` at $80 = $1,280 | **about $1,798** |

Cost per peak concurrent player per month: $5.00 at stage 1, $1.02 at stage 2, $0.45 at stage 3, $0.23 at stage 4.

Bundled transfer: stage 4 needs about 31 TB. The 16 game hosts alone include 16 x 4 TB = 64 TB pooled (Vultr pools allowances at the account level and adds 2 TB free), so the egress line is $0.

### 9.4 Ladder B: Hetzner EU best case, itemised (EUR, net of VAT)

Valid only if your account can order servers (section 1.1). Each server includes EUR 0.50 for its IPv4 address. Prices read on 2026-10-16: [Hetzner price adjustment](https://docs.hetzner.com/general/infrastructure-and-availability/price-adjustment/); specs: [Cost-Optimized page](https://www.hetzner.com/cloud/cost-optimized/).

| Stage | Line items | Monthly |
|---|---|---|
| 1 | 1 x CX23 (5.49 + 0.50) | **about EUR 6** |
| 2 | 1 x CX33 (4 vCPU / 8 GB) 8.99; 1 x CX23 staging 5.99; backups about 1 | **about EUR 16** |
| 3 | 3 x CX33 game 26.97; 1 x CX33 Postgres 8.99; 3 x CX23 (control, monitoring, staging) 17.97; backups 3 | **about EUR 57** |
| 4 | 9 x CX43 (8 vCPU / 16 GB) game 148.41; 2 x CX53 (16 vCPU / 32 GB) Postgres 59.98; 3 x CX23 gateways 17.97; 1 x CX33 monitoring 8.99; 2 x CX33 staging and CI 17.98; backups 18 | **about EUR 271** |
| 4, dedicated-vCPU Postgres | Replace the two CX53 with two CCX33 (EUR 138.49 + 0.50 each), adding about EUR 218 | **about EUR 489** |

Two warnings. First, these are shared vCPUs, so noisy neighbours (section 12) are a real risk at stage 3 and above; the dedicated-vCPU CCX line is far dearer per vCPU after the June increase (CCX33 is EUR 138.49 against EUR 15.99 for the 8 vCPU CX43; the 8-vCPU spec of CCX33 is from memory and was not re-verified). Second, a single provider that is currently capacity-restricted is a poor thing to build a five-thousand-player plan on. Treat Ladder B as the reason to keep your deployment portable, not as a promise.

### 9.5 Egress is the lever that decides the provider at stage 4

If all 31.1 TB a month at 5,000 CCU were billed at each provider's overage rate (my arithmetic from the unit rates read on 2026-10-16 in the sections above):

| Provider or service | Unit price | If all 31.1 TB were billed |
|---|---|---|
| Hetzner EU (overage) | about EUR 1 per TB (S) | about EUR 31 (in practice nothing: 20 TB is included per server) |
| Akamai / Linode | $0.005 per GB | about $156 |
| DigitalOcean | $0.01 per GiB | about $290 |
| Vultr | $0.01 per GB | about $311 (in practice $0: covered by pooled bundles) |
| Fly.io | $0.02 per GB (North America, Europe) | about $622 |
| Railway | $0.05 per GB | about $1,555 |
| AWS (Lightsail worked example; EC2 first tier per [egresscost](https://egresscost.com/aws), S) | $0.09 per GB | up to about $2,799 |

Choosing a host with large bundled transfer is worth more than any CPU optimisation at that scale. A 3x error in the bandwidth assumption (18 KB/s per player) would still fit inside the Ladder A fleet's pooled allowance of roughly 105 TB (16 x 4 TB game hosts, 2 x 8 TB database hosts, 3 x 3 TB gateways, 3 x 4 TB monitoring and staging, plus 2 TB free).

### 9.6 Sensitivity and what to do about it

- **Players per vCPU is the biggest unknown.** If the load test shows 180 per vCPU instead of 90, game-host cost at stage 4 halves (Ladder A total about $840). If it shows 45, it doubles (about $1,800).
- **30 Hz instead of 20 Hz** multiplies bandwidth by about 1.5 (A7) and CPU by about 1.5 (assumption). Decide the tick rate per zone type: 20 Hz is enough for most ARPG combat with client prediction; reserve 30 Hz for zones that need it.
- **More regions.** Each extra region splits players but repeats fixed items (gateways, monitoring agent, DB replica). Budget 15 to 25% extra in total for a second region, and treat each region as its own world (section 10).
- **Not in these numbers:** domain registration, paid DDoS add-ons, a load balancer for the TCP gateway if you add one, support plans, taxes, and the engineer-hours that dominate real cost beyond stage 2.

## 10. Latency guidance for an action RPG

### 10.1 What round-trip time is acceptable

I could not find an authoritative published latency threshold specific to action RPGs, and the per-genre figures from the Claypool and Claypool 2006 study (first-person, third-person and omnipresent games) could not be read from the sources the fetch tool could reach, so I do not quote them. The bands below are engineering judgement anchored on what I could verify:

- A general reference says a lag under 100 ms is considered necessary for playability and about 200 ms is distracting ([Wikipedia, Lag (video games)](https://en.wikipedia.org/wiki/Lag_(video_games)), S).
- With an authoritative server, delays of 100, 200 and even 500 ms are not unheard of, and half a second makes a game unplayable ([Gambetta, client-server architecture](https://www.gabrielgambetta.com/client-server-game-architecture.html), V).
- Remote entities are normally drawn slightly in the past (one update interval); the article calls a 100 ms offset generally unnoticeable, while precise actions suffer ([Gambetta, entity interpolation](https://www.gabrielgambetta.com/entity-interpolation.html), V). Server-side lag compensation rewinds the world to the moment the client acted, at the cost that a target may still be hit a few milliseconds after taking cover ([Gambetta, lag compensation](https://www.gabrielgambetta.com/lag-compensation.html), V).

Planning bands for a dodge-and-hit-confirm ARPG with client-side prediction, server authority and entity interpolation:

| Player RTT to the server | Experience | What to do |
|---|---|---|
| Under 40 ms | Feels local | Nothing |
| 40 to 80 ms | Good | Target for the median player |
| 80 to 120 ms | Fine with prediction and interpolation | Target for the 90th percentile |
| 120 to 180 ms | Playable; dodge timing and hit confirmation feel late | Give the server a lag-compensation window for melee and projectiles, lengthen enemy telegraphs, show ping in the UI |
| 180 to 250 ms | Poor | Warn the player and suggest a closer region |
| Over 250 ms | Not acceptable for action combat | Block or redirect |

How the delay adds up (all of it is on top of the player's RTT):

- **Your own actions** with client-side prediction: movement responds at once. Ability results, loot and trades are confirmed after one RTT plus the wait for the next server tick, which averages half a tick: 25 ms at 20 Hz and about 17 ms at 30 Hz.
- **Other players and monsters** are drawn about one snapshot interval plus one-way delay behind the server: one interval is 50 ms at 20 Hz and 33 ms at 30 Hz; buffering two snapshots for jitter doubles that. Choose the interpolation delay per zone type.
- **Jitter matters as much as the mean.** Judge a region by the 95th-percentile RTT and the packet loss, not by the average ping.

### 10.2 Reference round-trip times between regions

Azure's published median (P50) RTT between datacenters, dataset ending 2026-07-30 ([Azure network latency statistics](https://learn.microsoft.com/en-us/azure/networking/azure-network-latency), V). These are backbone figures between cloud regions. A real player adds their home network and ISP path, typically 10 to 40 ms (an estimate, not sourced), so treat these figures as a floor.

| Region pair | RTT (ms) |
|---|---|
| Central US and East US | 28 to 29 |
| Central US and West US | 39 |
| East US and West US | 66 to 69 |
| East US and North Europe | 73 to 76 |
| East US and UK South | 78 to 82 |
| East US and West Europe | 85 to 89 |
| East US and Germany West Central | 93 to 94 |
| East US and Brazil South | 117 to 118 |
| West US and North Europe | 133 |
| West US and UK South | 140 |
| West US and West Europe | 146 |
| West US and Germany West Central | 151 |
| East US and Japan East | 162 to 165 |
| East US and Southeast Asia | 224 to 228 |

### 10.3 Where to put regions

1. **Stage 1: one region, chosen by measurement.** Pick the metro with the lowest 95th-percentile RTT across your four players, not the geographic centre. Test with `ping -n 50 <address>` from each player's PC against a short-lived instance in each candidate metro, and compare averages, worst case and loss.
2. **United States only.** A central site (Chicago or Dallas, comparable to Azure's Central US region, which sits at or under 39 ms from either coast; Chicago and Dallas themselves were not measured) usually lands players at 50 to 80 ms. Two sites (East and West) cut the backbone leg to roughly the distance within one coast, a small fraction of the 66 to 69 ms East-West figure, and become worthwhile when many players are more than about 40 ms from the central site.
3. **United States plus Europe.** US East reaches Western Europe at roughly 73 to 94 ms backbone, so European players see about 100 to 130 ms. That is acceptable for a casual or PvE-focused ARPG and poor for precision combat. When Europe is more than about a fifth of your concurrency, add a European world rather than moving the whole game.
4. **Asia and Oceania.** US East to Southeast Asia is 224 ms or more on the backbone alone. Serve these players from a local region or accept that they play a different world.
5. **Do not place game hosts and Postgres in different regions.** Every save would pay a wide-area round trip. Keep the database in the same metro as the game hosts.

### 10.4 One world per region or one global world

| | One world per region (recommended) | One global world |
|---|---|---|
| Player latency | Low for everyone near a region | Fixed by the single location: far players suffer |
| Data model | Each region has its own character database; an account service can be global later | One database; sharding by zone does not help distance |
| Operations | Simple to reason about and to scale; failures stay local | A single failure domain for the whole player base |
| Social features | Friends in other regions need a cross-region mail, chat or party service, built later | Built in |
| Population risk | A region can feel empty; pooling by time zone helps | One large pool |
| Fits | An ARPG with real-time combat | Slow-paced or turn-based games |

For Skein: start with one region and one world. Add a second world in another region only when measured latency demands it, and keep account and authentication data separable from world data now so that decision stays cheap.

---

## 11. Bootstrap: the starter VPS, step by step

Target: one Ubuntu LTS VPS running Docker Compose with the Skein server and PostgreSQL, SSH by key only, a firewall, automatic security updates, log rotation, nightly off-provider backups, and a rollout procedure that costs players a few seconds. Commands assume the placeholder ports from the top of this file. `SERVER_IP` is the address the provider gives you.

### Step 1. Create the VM

- **Image:** Ubuntu 26.04 LTS (released 2026-04-23, standard support to May 2031, first point release 26.04.1 on 2026-08-27) or Ubuntu 24.04 LTS (standard support to June 2029). Docker Engine supports both ([Ubuntu releases](https://ubuntu.com/project/docs/release-team/list-of-releases/), [Docker install guide](https://docs.docker.com/engine/install/ubuntu/)). Choose x86_64.
- **Access:** paste your SSH public key into the provider's form at creation time. On your Windows PC, PowerShell can create the key:

```powershell
ssh-keygen -t ed25519 -C "skein-admin" -f "$env:USERPROFILE\.ssh\skein_ed25519"
Get-Content "$env:USERPROFILE\.ssh\skein_ed25519.pub"
```

- **Provider firewall** (Hetzner Cloud Firewall, DigitalOcean Cloud Firewall, Vultr Firewall, Lightsail networking tab): allow inbound TCP 22 from your own IP only, UDP 27500 and TCP 27501 from anywhere (or from your players' IPs during a closed alpha), ICMP, and all outbound. A provider firewall sits outside the VM, so Docker's iptables rules cannot bypass it (see step 5).
- **Name** the host `skein-01`. Enable IPv4; IPv6 is optional.

### Step 2. First login, updates, time zone

```bash
ssh -i ~/.ssh/skein_ed25519 root@SERVER_IP
apt update && apt full-upgrade -y
timedatectl set-timezone UTC
hostnamectl set-hostname skein-01
reboot        # only needed if the kernel was updated
```

### Step 3. Non-root admin user

```bash
adduser deploy                 # set a password; it is used for sudo only, SSH passwords are disabled in step 4
usermod -aG sudo deploy
install -d -m 700 -o deploy -g deploy /home/deploy/.ssh
cp /root/.ssh/authorized_keys /home/deploy/.ssh/authorized_keys
chown deploy:deploy /home/deploy/.ssh/authorized_keys
chmod 600 /home/deploy/.ssh/authorized_keys
```

Open a second terminal and confirm `ssh -i ~/.ssh/skein_ed25519 deploy@SERVER_IP` works and `sudo -v` succeeds before moving on.

### Step 4. SSH: keys only, no root

```bash
sudo tee /etc/ssh/sshd_config.d/10-skein.conf >/dev/null <<'EOF'
PermitRootLogin no
PasswordAuthentication no
KbdInteractiveAuthentication no
PubkeyAuthentication yes
AllowUsers deploy
MaxAuthTries 3
EOF
sudo sshd -t && sudo systemctl reload ssh
```

Keep the existing session open, then test a brand-new login in another terminal. Only then close the first one. (Some images ship a `50-cloud-init.conf` that sets `PasswordAuthentication yes`; drop-in files are read in lexical order and the first value wins, so the `10-` prefix above takes precedence.)

### Step 5. Host firewall (ufw) and the Docker bypass

```bash
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw limit 22/tcp                          # rate-limits SSH; or: sudo ufw allow from YOUR_HOME_IP to any port 22 proto tcp
sudo ufw allow 27500/udp comment 'Skein RakNet'
sudo ufw allow 27501/tcp comment 'Skein TCP'
sudo ufw enable
sudo ufw status verbose
```

**Important:** ports that Docker publishes are routed in the `nat` table before ufw's chains see them, so they bypass ufw rules, and Docker's install guide warns about exactly this ([Docker install guide](https://docs.docker.com/engine/install/ubuntu/), [packet filtering and firewalls](https://docs.docker.com/engine/network/packet-filtering-firewalls/)). The practical rules:

1. Publish only what must be public (UDP 27500, TCP 27501).
2. Publish Postgres and the HTTP admin port on `127.0.0.1` only (see the Compose file in step 11).
3. Do real restriction at the provider firewall (step 1), which Docker cannot bypass.

### Step 6. fail2ban for SSH

```bash
sudo apt install -y fail2ban
sudo tee /etc/fail2ban/jail.d/sshd-local.conf >/dev/null <<'EOF'
[sshd]
enabled  = true
backend  = systemd
maxretry = 4
findtime = 10m
bantime  = 1h
EOF
sudo systemctl enable --now fail2ban
sudo fail2ban-client status sshd
```

With key-only SSH, fail2ban mainly cuts log noise and wasted CPU. **Do not use it on the game UDP port**: UDP source addresses can be spoofed, so banning by source IP lets an attacker lock out your real players. Rate-limit handshake and unconnected-ping packets inside the server instead, which also limits reflection abuse.

### Step 7. Automatic security updates

```bash
sudo apt install -y unattended-upgrades          # installed by default on Ubuntu Server; harmless if present
sudo tee /etc/apt/apt.conf.d/52skein-unattended >/dev/null <<'EOF'
Unattended-Upgrade::Automatic-Reboot "true";
Unattended-Upgrade::Automatic-Reboot-Time "04:30";
Unattended-Upgrade::Remove-Unused-Kernel-Packages "true";
Unattended-Upgrade::Remove-Unused-Dependencies "true";
EOF
sudo unattended-upgrade --dry-run --debug 2>&1 | tail -n 15
```

By default the tool applies Ubuntu security updates and does not reboot; `Automatic-Reboot-Time` makes the reboot happen at a chosen local time instead of immediately ([Ubuntu Server docs](https://ubuntu.com/server/docs/how-to/software/automatic-updates/)). Pick a time when nobody plays; Docker's restart policy (step 12) brings the stack back. Docker's own packages come from Docker's repository and are not covered by this tool, so update them by hand every month or two.

### Step 8. Swap and UDP kernel settings (optional but cheap)

```bash
sudo fallocate -l 1G /swapfile && sudo chmod 600 /swapfile && sudo mkswap /swapfile && sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
sudo tee /etc/sysctl.d/90-skein.conf >/dev/null <<'EOF'
vm.swappiness = 10
net.core.rmem_max = 8388608
net.core.wmem_max = 8388608
net.core.netdev_max_backlog = 5000
EOF
sudo sysctl --system
```

Swap is a safety net against an out-of-memory kill during an image pull or a Postgres spike, not extra capacity. The buffer limits let the server request larger UDP socket buffers; watch `RcvbufErrors` in `/proc/net/snmp` (section 7.3).

### Step 9. Docker Engine and Compose

This is Docker's documented apt-repository method for Ubuntu ([Docker install guide](https://docs.docker.com/engine/install/ubuntu/)). Check that page for the current lines before pasting.

```bash
sudo apt update && sudo apt install -y ca-certificates curl
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc
sudo tee /etc/apt/sources.list.d/docker.sources >/dev/null <<EOF
Types: deb
URIs: https://download.docker.com/linux/ubuntu
Suites: $(. /etc/os-release && echo "${UBUNTU_CODENAME:-$VERSION_CODENAME}")
Components: stable
Signed-By: /etc/apt/keyrings/docker.asc
EOF
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo systemctl enable --now docker
sudo usermod -aG docker deploy          # log out and in again; note: docker group membership is root-equivalent
docker compose version
```

(The sources heredoc is unquoted on purpose, so that the `$(...)` expands to the Ubuntu codename; the other heredocs in this section are quoted so nothing expands.)

### Step 10. Log rotation

Docker's `json-file` driver does not rotate by default (`max-size` defaults to unlimited), so a chatty game server can fill the disk. The `local` driver rotates by default at 20 MB x 5 files with compression ([json-file](https://docs.docker.com/engine/logging/drivers/json-file/), [local](https://docs.docker.com/engine/logging/drivers/local/)). Make it the default for new containers and cap the systemd journal:

```bash
sudo tee /etc/docker/daemon.json >/dev/null <<'EOF'
{
  "log-driver": "local",
  "log-opts": { "max-size": "20m", "max-file": "5" }
}
EOF
sudo systemctl restart docker
sudo sed -i 's/^#\?SystemMaxUse=.*/SystemMaxUse=500M/' /etc/systemd/journald.conf
sudo systemctl restart systemd-journald
```

The setting applies to containers created after the restart. Have the server write logs to stdout only, so Docker handles rotation and the monitoring agent can read them.

### Step 11. Project layout and the Compose file

```bash
sudo mkdir -p /opt/skein /var/backups/skein
sudo chown -R deploy:deploy /opt/skein /var/backups/skein
cd /opt/skein
umask 077
cat > .env <<'EOF'
SKEIN_TAG=2026-10-16.1
POSTGRES_PASSWORD=replace-with-output-of-openssl-rand-hex-24
EOF
```

Build the image on your PC or in CI for the right platform and push it to a registry (GitHub Container Registry, Docker Hub, or any other); do not build on the small VPS:

```powershell
docker buildx build --platform linux/amd64 -t ghcr.io/YOUR_ORG/skein-server:2026-10-16.1 --push .
```

`/opt/skein/compose.yaml`:

```yaml
name: skein

x-logging: &logging
  driver: local
  options:
    max-size: "20m"
    max-file: "5"

services:
  db:
    image: postgres:18
    restart: unless-stopped
    environment:
      POSTGRES_DB: skein
      POSTGRES_USER: skein
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:?set it in .env}
    volumes:
      # PostgreSQL 18+ images: mount the PARENT directory (PGDATA is /var/lib/postgresql/18/docker).
      # For 17 and older the mount is /var/lib/postgresql/data. Using the wrong one loses data on re-create.
      - pgdata:/var/lib/postgresql
    ports:
      - "127.0.0.1:5432:5432"        # loopback only, never published on a public address
    shm_size: 256mb
    command: >
      postgres
      -c shared_buffers=256MB
      -c effective_cache_size=1GB
      -c max_connections=50
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U skein -d skein"]
      interval: 10s
      timeout: 3s
      retries: 6
    logging: *logging

  game:
    image: ghcr.io/YOUR_ORG/skein-server:${SKEIN_TAG:?set it in .env}
    restart: unless-stopped
    depends_on:
      db:
        condition: service_healthy
    environment:
      ConnectionStrings__Skein: "Host=db;Port=5432;Database=skein;Username=skein;Password=${POSTGRES_PASSWORD}"
    ports:
      - "27500:27500/udp"            # RakNet
      - "27501:27501/tcp"            # game TCP
      - "127.0.0.1:8080:8080/tcp"    # admin HTTP: reach it through an SSH tunnel or Tailscale
    stop_grace_period: 45s           # longer than your slowest save; also set HostOptions.ShutdownTimeout
    mem_limit: 2g
    ulimits:
      nofile:
        soft: 65536
        hard: 65536
    logging: *logging

volumes:
  pgdata:
```

Notes:

- The connection-string variable name above is an example; use whatever configuration key the server reads.
- Add a `healthcheck` to `game` only if the image contains a tool to run it. The default .NET runtime images do not include `curl`; a `--healthcheck` command line mode in the server itself is the clean way. With it, `docker compose up --wait` (step 15) can verify a rollout.
- Secrets in `.env` are acceptable at this size (`chmod 600`, never commit it, include it in the encrypted backup). Move to Docker secrets or SOPS-encrypted files when more than one person deploys.
- `network_mode: host` for the `game` service is a valid alternative that removes Docker's NAT and conntrack from the UDP path and makes ufw rules apply directly. Use it if profiling shows packet-path overhead or conntrack table pressure, and then connect to Postgres at `127.0.0.1:5432`.

Dockerfile sketch for the RakNet host (untested, because the host program is a placeholder today; build it in CI, not on the VPS):

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Skein.Server.Host.Brutal -c Release -r linux-x64 --self-contained false -o /out

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
ENTRYPOINT ["./skein-server-brutal"]
```

The assembly name `skein-server-brutal` comes from the project file, `-r linux-x64` selects the native RakNet library that the package ships, and `InvariantGlobalization` is already on in the project file, so no ICU packages are needed. The default Microsoft runtime images are Debian based, which is what section 1.6 recommends over Alpine. Check Microsoft's current tag list before pinning.

### Step 12. Start on boot: restart policy plus an optional systemd unit

`restart: unless-stopped` plus `systemctl enable docker` already brings both containers back after a reboot or a crash, and keeps them stopped if you stopped them on purpose. Add a systemd unit if you also want `systemctl start|stop|restart skein` to bring the whole stack up (for example after a `docker compose down`):

```bash
sudo tee /etc/systemd/system/skein.service >/dev/null <<'EOF'
[Unit]
Description=Skein game stack (docker compose)
Requires=docker.service
After=docker.service network-online.target
Wants=network-online.target

[Service]
Type=oneshot
RemainAfterExit=yes
User=deploy
Group=docker
WorkingDirectory=/opt/skein
ExecStart=/usr/bin/docker compose up -d --wait --wait-timeout 120
ExecStop=/usr/bin/docker compose stop -t 45
TimeoutStartSec=300
TimeoutStopSec=90

[Install]
WantedBy=multi-user.target
EOF
sudo systemctl daemon-reload
sudo systemctl enable --now skein.service
```

Start the stack the first time with `cd /opt/skein && docker compose up -d --wait`, then `docker compose ps` and `docker compose logs -f game`.

### Step 13. Nightly pg_dump to object storage, and a restore drill

Install rclone, create a bucket at a provider other than the game host (R2 or B2, section 8), and create an API key scoped to that bucket only. Put the credentials in a mode-600 env file readable only by the deploy user, using rclone's environment-variable form of a remote called `offsite`. R2 example:

```bash
sudo apt install -y rclone
sudo install -m 600 -o deploy -g deploy /dev/null /etc/skein-backup.env
sudo tee /etc/skein-backup.env >/dev/null <<'EOF'
RCLONE_CONFIG_OFFSITE_TYPE=s3
RCLONE_CONFIG_OFFSITE_PROVIDER=Cloudflare
RCLONE_CONFIG_OFFSITE_ACCESS_KEY_ID=REPLACE
RCLONE_CONFIG_OFFSITE_SECRET_ACCESS_KEY=REPLACE
RCLONE_CONFIG_OFFSITE_ENDPOINT=https://ACCOUNT_ID.r2.cloudflarestorage.com
BACKUP_BUCKET=skein-backups
HC_UUID=your-healthchecks-io-uuid
EOF
```

`/usr/local/bin/skein-backup.sh` (mode 755):

```bash
#!/usr/bin/env bash
set -euo pipefail
cd /opt/skein
STAMP=$(date -u +%Y%m%dT%H%M%SZ)
OUT=/var/backups/skein/skein-${STAMP}.dump

# 1. dump in PostgreSQL custom format (already compressed)
docker compose exec -T db pg_dump -U skein -d skein -Fc --no-owner > "${OUT}.partial"

# 2. verify the archive can be read, then publish it
docker compose exec -T db pg_restore --list < "${OUT}.partial" > /dev/null
mv "${OUT}.partial" "${OUT}"

# 3. optional: encrypt with age before upload (backups hold player data)
#    age -R /etc/skein-backup-recipients.txt -o "${OUT}.age" "${OUT}" && OUT="${OUT}.age"

# 4. upload to a bucket at a different provider
rclone copyto "${OUT}" "offsite:${BACKUP_BUCKET}/pg/$(basename "${OUT}")" --s3-no-check-bucket

# 5. keep three days locally; remote retention is a bucket lifecycle rule (for example 30 days)
find /var/backups/skein -name 'skein-*.dump*' -mtime +3 -delete

# 6. dead-man's switch: alerts if no ping arrives in 26 hours
curl -fsS -m 10 --retry 3 "https://hc-ping.com/${HC_UUID}" > /dev/null
```

Schedule it with a systemd timer (it catches up after downtime thanks to `Persistent=true`):

```bash
sudo chmod 755 /usr/local/bin/skein-backup.sh
sudo tee /etc/systemd/system/skein-backup.service >/dev/null <<'EOF'
[Unit]
Description=Skein nightly pg_dump to object storage
After=docker.service

[Service]
Type=oneshot
User=deploy
Group=docker
EnvironmentFile=/etc/skein-backup.env
ExecStart=/usr/local/bin/skein-backup.sh
EOF
sudo tee /etc/systemd/system/skein-backup.timer >/dev/null <<'EOF'
[Unit]
Description=Run the Skein backup nightly

[Timer]
OnCalendar=*-*-* 04:10:00 UTC
RandomizedDelaySec=300
Persistent=true

[Install]
WantedBy=timers.target
EOF
sudo systemctl daemon-reload
sudo systemctl enable --now skein-backup.timer
sudo systemctl start skein-backup.service && journalctl -u skein-backup.service -n 20 --no-pager
```

Restore drill (do this once now, then monthly), using a throwaway database inside the same container:

```bash
cd /opt/skein
LATEST=$(ls -1t /var/backups/skein/skein-*.dump | head -n 1)
docker compose exec -T db createdb -U skein skein_restore_test
docker compose exec -T db pg_restore -U skein -d skein_restore_test --no-owner < "$LATEST"
docker compose exec -T db psql -U skein -d skein_restore_test -c '\dt'
docker compose exec -T db dropdb -U skein skein_restore_test
```

Full disaster recovery is: create a new VM, run steps 1 to 12, copy the newest dump down from the bucket with rclone, restore it into `skein`, `docker compose up -d`, and repoint DNS. Time one rehearsal; that is your real recovery time.

### Step 14. Monitoring agent

For stages 1 and 2, create a Grafana Cloud Free stack ([limits](https://grafana.com/pricing/)) and run Grafana's collector (Alloy) as one more container or a systemd service, following Grafana's own installation guide (not reproduced here). Scrape the server's metrics endpoint, node metrics, and Docker container logs. Keep per-player labels out of metrics to stay under 10k series. Create the alerts listed in section 7.3, plus the healthchecks.io check for the backup job.

### Step 15. Rolling a new server version with near-zero downtime

What "near-zero" means here: with one authoritative process holding the world in memory, a swap costs a few seconds of reconnect, not zero. For a handful of players that is the right trade. The ingredients:

1. **Graceful shutdown.** On SIGTERM the server stops accepting logins, broadcasts a notice, saves every player and zone to Postgres, closes sockets, and exits. Set .NET `HostOptions.ShutdownTimeout` and the Compose `stop_grace_period` (45 s above) to more than your slowest save.
2. **Reconnect in the client.** A session token plus automatic retry with backoff turns the restart into a short "Reconnecting..." overlay.
3. **Pre-pull the image** so the swap does not wait on the network.
4. **Expand-then-contract database migrations.** Apply additive schema changes first so the old and new server versions both work, and remove old columns only in a later release.
5. **Protocol version in the handshake**, so an outdated client gets a clear message instead of a crash.

`/opt/skein/deploy.sh`:

```bash
#!/usr/bin/env bash
# usage: ./deploy.sh 2026-10-17.1
set -euo pipefail
TAG="${1:?usage: deploy.sh <image-tag>}"
cd /opt/skein
PREV=$(grep '^SKEIN_TAG=' .env | cut -d= -f2)

sed -i "s/^SKEIN_TAG=.*/SKEIN_TAG=${TAG}/" .env
docker compose pull game                         # 1. no downtime: image is now local

# 2. optional: run additive migrations with the new image before the swap
# docker compose run --rm --no-deps game --migrate

# 3. announce through your admin API or a chat command, then wait 30-60 s

# 4. swap only the game container; Postgres keeps running
if ! docker compose up -d --no-deps --wait --wait-timeout 90 game; then
  echo "new version failed its health check, rolling back to ${PREV}" >&2
  sed -i "s/^SKEIN_TAG=.*/SKEIN_TAG=${PREV}/" .env
  docker compose up -d --no-deps --wait game
  exit 1
fi
docker image prune -f > /dev/null
```

True blue/green (two game containers on different UDP ports while players drain from one to the other) needs two things Skein may not have yet: a directory the TCP gateway uses to tell clients which UDP port to join, and a single-owner rule for each zone so that two processes never simulate and save the same zone at once (for example a Postgres advisory lock per zone). Build both during stage 3 and the rollout becomes: start the new version beside the old, move new logins, drain the old, stop it.

### Step 16. Verify

```bash
sudo ss -lunp | grep 27500                  # UDP listener is up (published by docker-proxy or bound by the container)
sudo ss -ltnp | grep -E '27501|8080|5432'   # 8080 and 5432 must show 127.0.0.1, not 0.0.0.0
sudo ufw status verbose
sudo fail2ban-client status sshd
sudo systemctl list-timers --all | grep skein
docker compose ps && docker stats --no-stream
sudo tcpdump -ni any udp port 27500 -c 20   # while a player connects: packets in and replies out
```

From outside: connect with the real client, then reboot the VM once to prove everything comes back by itself and the first scheduled backup lands in the bucket.

---

## 12. Risks and gotchas

| # | Risk | What can go wrong | Evidence (V unless marked) | Mitigation |
|---|---|---|---|---|
| 1 | Capacity and price shocks at the cheapest provider | You cannot create or resize servers when you need to, or a rescale re-prices an old server at the new, much higher rate | Hetzner status notice open since 2026-06-26; June 2026 increases from about +30% (CAX41) to about +209% (US CPX41), my arithmetic from the old and new EUR columns; rescaling moves legacy-priced servers to current prices ([price adjustment](https://docs.hetzner.com/general/infrastructure-and-availability/price-adjustment/), [billing FAQ](https://docs.hetzner.com/cloud/billing/faq/)) | Keep Docker images, Compose file and the bootstrap in version control; use a DNS name with a low TTL; keep backups at another provider; know your fallback provider before you need it |
| 2 | Noisy neighbours on shared vCPU | Tick p99 spikes for no reason you can see in your own code | DigitalOcean says a shared Droplet may get fractions of a hyper-thread when neighbours are busy. Fly shared vCPUs have a 6.25% baseline and throttle when the burst balance is empty. Lightsail baselines are 5 to 20% per vCPU on the small bundles (all V, links in sections 2 and 3) | Alert on CPU steal and tick p99; leave 40% headroom (A3); move game hosts to dedicated-vCPU plans from stage 3 if steal shows up (Vultr Optimized `voc-c-4c-8gb-75s-amd` is $80/month) |
| 3 | Egress charges | A surprise bill, or a throttled host | Overage rates from $0.005/GB (Akamai) to $0.09/GB (AWS) (section 9.5). Bundled allowances range from 1 TB (Hetzner US CPX11) to 20 TB (Hetzner EU) per server | Pick a provider with large bundled transfer; track bytes per player (section 7.3); set billing alerts |
| 4 | IPv4 pricing | A line item that did not exist before | Hetzner EUR 0.50 / $0.60 per IPv4 per month; AWS $0.005 per hour per in-use public IPv4 (about $3.65/month); DigitalOcean "v5" configurations list IPv4 at an introductory $0.00 that may change; Lightsail IPv6-only bundles are $1.50 to $4 cheaper than dual-stack, and Vultr's IPv6-only 0.5 GB plan is $1.00 cheaper than the IPv4 one (V) | Budget one IPv4 per server; give gateways and game hosts IPv4, keep databases and monitoring private where the provider supports private networking |
| 5 | UDP through NAT | Players behind strict NAT or CGNAT drop out; home hosting fails behind CGNAT; clients lose their mapping while idle | Typical consumer NAT mappings for UDP expire after tens of seconds to a few minutes (general networking knowledge, not sourced here); 100.64.0.0/10 is carrier-grade-NAT space ([RFC 6598](https://www.rfc-editor.org/rfc/rfc6598)) | Send keepalives every 10 to 15 seconds; the server should be on a public IPv4; handle roaming addresses by session token, not by source IP |
| 6 | Docker bypasses ufw | A database or admin port is public although ufw says denied | Docker docs, see step 5 | Publish Postgres and admin on 127.0.0.1; enforce with the provider firewall |
| 7 | DDoS and blackholing | The provider drops all traffic to your IP during a large flood | DigitalOcean blackholes all inbound traffic to the target IP when an attack exceeds mitigation capacity; Vultr adds 10 Gbps of mitigation per instance with rerouting in about 60 seconds; Hetzner and OVHcloud advertise always-on filtering (links in section 2) | Do not reuse the game IP for other public services; ask support what happens at your plan's mitigation limit before launch; consider a larger mitigation tier once the game has real players |
| 8 | Abuse and reflection | Your server answers spoofed unconnected pings, so it can be used to reflect traffic, and providers may then limit or suspend the host. Separately, a provider's automated flood filters could misclassify your own bursts of legitimate UDP and drop them | Standard UDP amplification risk (general networking knowledge, not sourced here). I found no provider documentation describing game-specific filtering, so the second risk is a test-it risk | Rate-limit unconnected and handshake packets per source, keep unauthenticated responses small, and never answer a source more bytes than it sent. Run the 24-hour bot swarm from section 13 on the real provider before launch and ask support about UDP rate limits |
| 9 | Free-tier reclamation | The free server vanishes | Oracle may reclaim an Always Free instance idle for 7 days under 20% CPU p95, network and (A1) memory (V) | Do not run the only copy of anything on it |
| 10 | Wrong Postgres volume path | Data silently lost when the container is re-created | PostgreSQL 18 images use `/var/lib/postgresql` as the volume and a versioned PGDATA; 17 and below use `/var/lib/postgresql/data` ([Docker Hub](https://hub.docker.com/_/postgres)) | Use the Compose file in step 11 and run a restore drill |
| 11 | .NET end of support | Base images stop receiving security fixes, and the shipped server runs on an unsupported runtime | .NET 8 (LTS) and .NET 9 (STS) both reach end of support on 2026-11-10; .NET 10 (LTS) is supported until 2028-11-14 ([Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)). In this repository the libraries, the pure-.NET host and the tests target net8.0, and only the BRUTAL/RakNet host is .NET 10 (section 1.6) | Retarget every project to net10.0 before the first production build; use `mcr.microsoft.com/dotnet/runtime:10.0` style images (check the current tag list), pin tags and rebuild monthly |
| 12 | Arm instead of x64 | Cheap or free Arm plans (Oracle A1, Hetzner CAX) cannot run the RakNet host | `Ahwoo.Brutal.RakNet` 2026.10.2 lists native libraries for linux-x64, osx-arm64 and win-x64 only (section 1.6; local restore output) | Stay on x86_64 plans unless the vendor ships a linux-arm64 build |
| 13 | Single box | Provider maintenance, a bad kernel update, or a disk fault ends the session | n/a | Accept it at stages 1 and 2; automate rebuild; from stage 3 run N+1 game hosts |
| 14 | Managed-service rule changes | A managed database stops offering what you planned | DigitalOcean: from 2026-10-15 for new accounts, new Standard Edition PostgreSQL clusters cannot exceed 4 GiB RAM or add standby nodes ([limits doc](https://docs.digitalocean.com/products/databases/postgresql/details/limits/)) | Read the edition rules before you design HA around a managed service |
| 15 | Currency and tax | Bills 19 to 25% higher than the table in many EU countries | Hetzner quotes are net of VAT; US providers exclude sales tax | Add your local VAT or sales tax to every figure |


## 13. What to measure next (turns the assumptions into facts)

Run these before stage 2 and again before stage 3. A $20 test VPS in the same metro as the game host, billed by the hour, is enough for the load generator.

1. **Bot swarm.** A headless client that logs in, moves, fights and uses skills. Ramp 10, 50, 100, 150 bots per zone process at your real tick rate and record tick p50/p95/p99, CPU, RSS and GC pauses. Output: the real "players per vCPU" for A2 to A4.
2. **Bytes per player.** Record bytes and packets in and out per player per second in town, in combat, and in a crowded zone. Output: the real value for A7 and A8. Compare with `node_network_transmit_bytes_total` to confirm the egress model.
3. **Zone transfer cost.** Time a handoff between zone processes (save, release, reconnect, load). Output: whether the loading screen is acceptable and whether you can drain a host for a rollout.
4. **Persistence.** Write rate and p99 latency of saves at 150 bots, and the time to restore a nightly dump. Output: A10 and your real recovery time.
5. **Idle cost of the BRUTAL job runner.** With zero players connected, record CPU per vCPU and thread count for 10 minutes. If worker threads spin, the idle load must be subtracted from "CPU per player" and burstable or low-baseline plans (Lightsail, Fly shared) are ruled out. Output: the fixed cost per zone process for A2.
6. **Jitter on the actual VPS.** Run the bot swarm for 24 hours and look at the 99.9th-percentile tick time and CPU steal. Output: whether shared vCPUs are good enough or you need dedicated ones.
7. **Real player RTT.** Collect each player's RTT and loss to the server for a week. Output: whether the chosen metro is right (section 10).

Decisions these numbers drive: stage thresholds in section 9.2, whether to buy dedicated vCPUs, tick rate per zone type, whether to add a second region, and when (if ever) Agones is worth it.

---

## 14. Sources

Every URL below was opened on **2026-10-16** (the access date) unless a different date is stated. "V" and "S" follow the legend at the top. Search-engine result summaries were used to find pages; statements in this document rest on the pages listed here.

### 14.1 Hetzner

- https://docs.hetzner.com/general/infrastructure-and-availability/price-adjustment/ : cloud prices before and after 2026-06-15; page last changed 2026-07-08 (V)
- https://www.hetzner.com/cloud/cost-optimized/ : CX and CAX specs, EU traffic, "currently unavailable" labels (V)
- https://www.hetzner.com/cloud/regular-performance/ : CPX specs, traffic per location, "currently unavailable" labels (V)
- https://status.hetzner.com/ : open notices and incidents on 2026-10-16 (V)
- https://status.hetzner.com/incident/0a75c7ae-3377-41dc-aabe-601063724d24 : "Limited availability of cloud instances", start 2026-06-26, state identified (V)
- https://docs.hetzner.com/cloud/general/faq : why servers cannot be ordered, temporary location restrictions; last change 2026-04-27 (V)
- https://docs.hetzner.com/cloud/servers/faq/ : availability is rechecked about every 15 minutes (V)
- https://docs.hetzner.com/cloud/billing/faq/ : hourly billing with monthly cap, traffic billing, re-pricing rules (V)
- https://docs.hetzner.com/cloud/servers/primary-ips/overview/ : IPv4 EUR 0.50 / $0.60 per month, IPv6 free (V)
- https://docs.hetzner.com/cloud/servers/overview/ : server type families and locations; no availability notice on that page (V)
- https://www.hetzner.com/unternehmen/ddos-schutz/ : DDoS protection statement (V)
- https://docs.hetzner.com/storage/object-storage/overview/ : Object Storage billing mechanics, locations; no prices on the page (V)
- https://egresscost.com/hetzner : traffic overage about EUR 1/TB, US servers at least 1 TB included; data read 2026-09-30 (S)
- https://privatedevops.com/news/hetzner-june-2026-cloud-price-increase-what-to-do : June 2026 price change summary, "fourth pricing action" claim (S)
- https://findstack.com/resources/hetzner-price-increase-2026 : 2026 repricing and unavailable CX/CAX plans, article dated 2026-09-16 (S)
- https://findstack.com/resources/is-hetzner-reliable : capacity notice analysis, article dated 2026-09-28 (S)
- https://sliplane.io/blog/hetzner-object-storage-alternatives : Object Storage price "as of July 2026" (S)
- https://frontend.sliplane.io/blog/5-cheap-ways-to-host-postgres : Hetzner has no managed Postgres (S; seen in search results, not opened in full)
- Opened but yielded no prices or specs: https://www.hetzner.com/cloud/ , https://www.hetzner.com/storage/object-storage/ , https://www.hetzner.com/storage/object-storage/pricing/ , https://www.hetzner.com/dedicated-rootserver/matrix-ax/

### 14.2 DigitalOcean

- https://www.digitalocean.com/pricing/droplets : Basic Droplet plans and prices (V)
- https://docs.digitalocean.com/products/droplets/details/pricing/ : overage $0.01/GiB, pooled transfer, 672-hour cap, IPv4 note; last verified 25 Aug 2026 (V)
- https://docs.digitalocean.com/platform/regional-availability/ : data centers and CPU limitations (V)
- https://docs.digitalocean.com/products/droplets/concepts/choosing-a-plan/ : shared versus dedicated CPU (V)
- https://docs.digitalocean.com/products/networking/ddos/ : DDoS protection and blackholing (V)
- https://docs.digitalocean.com/products/databases/postgresql/details/pricing/ : managed PostgreSQL prices, edition change; last verified 2026-09-30 (V)
- https://docs.digitalocean.com/products/databases/postgresql/details/limits/ : connections, PITR, Standard versus Advanced Edition (V)
- https://www.digitalocean.com/pricing/managed-databases : managed PostgreSQL price table (V)
- https://www.digitalocean.com/pricing/kubernetes : DOKS control plane free, HA $40/month (V)

### 14.3 Vultr

- https://api.vultr.com/v2/plans?type=vc2&per_page=25 : Regular plans, prices, locations (V; public API)
- https://api.vultr.com/v2/plans?type=vhf&per_page=25 : High Frequency plans (V)
- https://api.vultr.com/v2/plans?type=vhp&per_page=25 : High Performance plans, read for context (V)
- https://api.vultr.com/v2/plans?type=voc&per_page=25 : Optimized Cloud Compute (dedicated vCPU), first 25 of 51 plans (V)
- https://api.vultr.com/v2/regions?per_page=100 : region list (V)
- https://docs.vultr.com/support/platform/billing/what-is-the-bandwidth-overage-rate : $0.01/GB overage; updated 2025-12-16 (V)
- https://blogs.vultr.com/Vultr-Announces-Reduced-Bandwidth-Pricing-2-Tb-Of-Free-Monthly-Egress-Free-Ingress-And-Global-Pooling : 2 TB free outbound, global pooling; dated 2022-11-30 (V)
- https://docs.vultr.com/ddos-protection : 10 Gbps mitigation per instance (V)
- https://egresscost.com/vultr : confirmation of the 2 TB pool and rates; data read 2026-09-30 (S)
- Returned HTTP 403 to the fetch tool, so not used: https://www.vultr.com/pricing/ , https://www.vultr.com/products/cloud-compute/

### 14.4 Akamai / Linode, AWS, Oracle, OVHcloud

- https://www.akamai.com/cloud/pricing/cpu : shared and dedicated plan prices, $0.005/GB overage (V)
- https://api.linode.com/v4/linode/types?page_size=100 : plan catalogue (V; public API)
- https://api.linode.com/v4/regions?page_size=100 : regions (V)
- https://www.akamai.com/cloud/pricing and https://www.akamai.com/why-akamai/global-infrastructure : opened, no plan tables or region cities on them
- https://aws.amazon.com/lightsail/pricing/ : Lightsail bundles (V)
- https://docs.aws.amazon.com/lightsail/latest/userguide/baseline-cpu-performance.html : burst baselines per bundle (V)
- https://docs.aws.amazon.com/lightsail/latest/userguide/understanding-regions-and-availability-zones-in-amazon-lightsail.html : Lightsail regions (V)
- https://aws.amazon.com/vpc/pricing/ : public IPv4 $0.005 per hour (V)
- https://aws.amazon.com/eks/pricing/ : EKS $0.10 per cluster-hour (V)
- https://egresscost.com/aws : EC2 egress $0.09/GB first tier, 100 GB free; verified 2026-09 (S)
- https://aws.amazon.com/ec2/pricing/on-demand/ : opened, data-transfer tier table not shown
- https://docs.oracle.com/en-us/iaas/Content/FreeTier/freetier_topic-Always_Free_Resources.htm : Always Free allowances, idle reclamation, home region (V)
- https://infoq.com/news/2026/07/oracle-cloud-free-tier-limits/ : Always Free Arm allowance halved; article dated 2026-07-03 (S)
- https://www.oracle.com/cloud/free/ : returned HTTP 403, not used
- https://us.ovhcloud.com/vps/ and https://www.ovhcloud.com/en/vps/ : VPS "from" prices and specs, unlimited traffic, anti-DDoS claim (S/U for terms)

### 14.5 Platform-as-a-service

- https://docs.fly.io/networking/udp-and-tcp : UDP requirements on Fly.io (V)
- https://docs.fly.io/about/pricing : Machines, volumes, IPv4, egress prices (V)
- https://docs.fly.io/machines/cpu-performance : shared versus performance vCPU behaviour (V)
- https://docs.fly.io/postgres/ : Managed Postgres starting price (V)
- https://docs.railway.com/networking/tcp-proxy : TCP proxy (V)
- https://docs.railway.com/reference/pricing/plans : plans and usage rates (V)
- https://station.railway.com/feedback/udp-inboud-connections-9f036ffc : inbound UDP request, status under review (V for the status; replies dated up to 2026-07-21)
- https://render.com/docs/web-services and https://render.com/docs/private-services : HTTP-only public ingress, private services (V)
- Opened without useful data: https://docs.railway.com/reference/public-networking , https://render.com/pricing , https://render.com/docs/private-network , https://render.com/docs/faq

### 14.6 Home hosting and Cloudflare

- https://tailscale.com/kb/1154/free-plans-discounts : Personal plan, 6 users; last validated 2026-04-08 (V)
- https://tailscale.com/pricing : Personal and Standard plans (V)
- https://tailscale.com/kb/1257/connection-types : direct versus relayed connections (V)
- https://tailscale.com/docs/reference/troubleshooting/network-configuration/tcp-connection-two-devices : MTU 1280 (V)
- https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/routing-to-tunnel/protocols/ : protocols for published applications, no UDP (V)
- https://developers.cloudflare.com/spectrum/ : custom TCP/UDP applications need Enterprise (V)
- https://developers.cloudflare.com/fundamentals/reference/network-ports/ : proxied ports; Spectrum for all TCP and UDP ports is Enterprise only (V)
- https://blog.cloudflare.com/extending-cloudflares-zero-trust-platform-to-support-udp-and-internal-dns/ : UDP for private networks announced 2021-12-08 (S for current status)
- https://www.rfc-editor.org/rfc/rfc6598 : 100.64.0.0/10 shared address space for carrier-grade NAT (V)
- https://www.duckdns.org/ : free dynamic DNS (V)
- Opened without useful data: https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/ , https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/get-started/ , https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/routing-to-tunnel/ , https://developers.cloudflare.com/cloudflare-one/networks/routes/ , https://www.cloudflare.com/plans/zero-trust-services/

### 14.7 PostgreSQL, orchestration, observability, backups

- https://neon.com/pricing and https://neon.com/docs/introduction/plans : Neon Free and Launch (V)
- https://supabase.com/pricing : Supabase Free and Pro (V)
- https://hub.docker.com/_/postgres : supported major versions, PostgreSQL 18 volume path (V)
- https://agones.dev/site/docs/ , https://agones.dev/site/docs/installation/ , https://agones.dev/site/docs/guides/client-sdks/ , https://agones.dev/site/docs/reference/gameserver/ : what Agones is, release 1.61 and supported Kubernetes versions, SDK calls, port policies (V)
- https://agones.dev/site/docs/installation/creating-cluster/ : opened, no networking details
- https://github.com/googleforgames/agones/releases : v1.61.0 listed as latest, dated 24 Sep (year not shown on the page) (V)
- https://grafana.com/pricing/ : Grafana Cloud Free and Pro (V)
- https://grafana.com/docs/loki/latest/get-started/overview/ : Loki overview (V)
- https://prometheus.io/docs/prometheus/latest/storage/ : disk sizing, bytes per sample, default retention (V)
- https://healthchecks.io/pricing/ : free plan limits (V)
- https://healthchecks.io/docs/http_api/ : ping URL format (`https://hc-ping.com/<uuid>`, `/fail` suffix) used in the backup script (V)
- https://developers.cloudflare.com/r2/examples/rclone/ : rclone configuration for R2 (provider, endpoint form, `no_check_bucket` for object-scoped tokens) (V)
- https://rclone.org/docs/ : defining a remote through `RCLONE_CONFIG_<NAME>_<OPTION>` environment variables (V)
- https://developers.cloudflare.com/r2/pricing/ : R2 prices and free tier (V)
- https://www.backblaze.com/cloud-storage/pricing : B2 prices, free egress rule (V; page shows no effective date)

### 14.8 Latency and bootstrap references

- https://learn.microsoft.com/en-us/azure/networking/azure-network-latency : P50 RTT between Azure regions, dataset ending 2026-07-30, page updated 2026-08-20 (V)
- https://www.gabrielgambetta.com/client-server-game-architecture.html , https://www.gabrielgambetta.com/entity-interpolation.html , https://www.gabrielgambetta.com/lag-compensation.html : authoritative servers, interpolation, lag compensation (V)
- https://en.wikipedia.org/wiki/Lag_(video_games) : general lag thresholds (S)
- Could not be read, so the Claypool and Claypool per-genre latency thresholds are not quoted: https://web.cs.wpi.edu/~claypool/papers/lat-chi/ (404), https://cacmb4.acm.org/magazines/2006/11/5798-latency-and-player-actions-in-online-games (403), https://cacm.acm.org/?p=1673 (403), https://developer.valvesoftware.com/wiki/Latency_Compensating_Methods_in_Client/Server_In-game_Protocol_Design_and_Optimization (403); PDF versions at https://ftp.cs.wpi.edu/pub/techreports/pdf/06-13.pdf and https://sites.cs.ucsb.edu/~almeroth/classes/W10.290F/papers/claypool-10.pdf came back as unreadable binary
- https://ubuntu.com/project/docs/release-team/list-of-releases/ : Ubuntu 26.04 LTS and 24.04 LTS dates (V)
- https://docs.docker.com/engine/install/ubuntu/ : supported Ubuntu versions, apt repository method, firewall warning (V)
- https://docs.docker.com/engine/network/packet-filtering-firewalls/ : published ports bypass ufw (V)
- https://docs.docker.com/engine/logging/drivers/json-file/ and https://docs.docker.com/engine/logging/drivers/local/ : log rotation defaults (V)
- https://ubuntu.com/server/docs/how-to/software/automatic-updates/ : unattended-upgrades options (V)
- https://ubuntu.com/server/docs/how-to/security/firewalls/ : ufw basics (V)
- https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core : .NET 8, 9, 10 support dates (V)

### 14.9 Limits of this research

- The fetch tool summarises pages with a small model, so figures can be mis-transcribed. I cross-checked key numbers against a second page or a public API response where I could (Hetzner prices against secondary articles, Vultr and Linode prices against their public plan APIs and the Akamai page, DigitalOcean prices against a second page), and where sources disagreed (DigitalOcean PostgreSQL $15.00 versus $15.15, Hetzner Object Storage EUR 4.99 / 5.99 / 6.49) both figures are shown.
- Several vendor pricing pages render prices with JavaScript or block automated fetches. Where that stopped me (Hetzner Object Storage, OVHcloud commitment terms, Render plans, Vultr marketing page, DigitalOcean Premium CPU plans, GKE and EKS Auto Mode details, Hetzner dedicated servers) I said so and asserted no price.
- Players-per-vCPU, bytes-per-player and average-concurrency figures are planning assumptions, not measurements.
- The apt repository lines in step 9 follow Docker's install guide (section 14.8), and the R2 endpoint form in step 13 follows Cloudflare's rclone guide (section 14.7). The commands in section 11 were not executed on a real server while preparing this document; test them on a throwaway VM first.
- No account was created and nothing was purchased or deployed while preparing this document.

### 14.10 Repository files read (local, read-only, 2026-10-16)

- `global.json` : SDK 8.0.100 with `rollForward: latestMajor`
- `Directory.Build.props` and the `*.csproj` files under `src/` and `tests/` : target frameworks (net8.0 everywhere except the BRUTAL host)
- `src/Skein.Server.Host.Brutal/Skein.Server.Host.Brutal.csproj` : BRUTAL packages are .NET 10 only; package versions
- `src/Skein.Server.Host.Brutal/obj/project.assets.json` : native runtime list of `Ahwoo.Brutal.RakNet` 2026.10.2 (linux-x64, osx-arm64, win-x64)
- `src/Skein.Server.Host/Program.cs` and `src/Skein.Server.Host.Brutal/Program.cs` : both are placeholders at the time of reading
- Nothing in the repository was modified. The only file created is this one (the folder `docs/infrastructure` was created for it).
