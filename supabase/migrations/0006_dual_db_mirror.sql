-- Dual-DB transition layer.
--
-- The new app talks ONLY to this project. A scheduled job (mirror-legacy edge
-- function) pulls the legacy "Gridzero" project's rows (read-only, via its
-- public anon key) into the private legacy_mirror schema. Public views then
-- present a MERGED (native + legacy) read surface so returning players see the
-- full leaderboard and every plane they ever owned, while legacy installs keep
-- writing to the legacy DB untouched.
--
-- legacy_mirror is NOT in the API-exposed schemas, so PostgREST/anon cannot
-- read it directly; only the definer views in public surface a curated subset.

create schema if not exists legacy_mirror;

-- ---- Mirror tables: keyed by the legacy natural unique keys so the puller's
-- ---- upserts are idempotent (device_id for players, tx_signature for ledgers).
create table if not exists legacy_mirror.pl_players (
    pl_device_id    text primary key,
    pl_wallet       text,
    pl_total_coins  integer,
    pl_high_score   integer,
    pl_plane_id     integer,
    pl_sound_on     boolean,
    pl_planes_owned integer,
    pl_created_at   timestamptz,
    pl_updated_at   timestamptz,
    mirrored_at     timestamptz not null default now()
);
create index if not exists lm_players_wallet_idx
    on legacy_mirror.pl_players (pl_wallet) where pl_wallet is not null;

create table if not exists legacy_mirror.pl_leaderboard (
    pl_tx_signature text primary key,
    pl_wallet       text not null,
    pl_score        integer not null,
    pl_cluster      text,
    pl_created_at   timestamptz,
    mirrored_at     timestamptz not null default now()
);

create table if not exists legacy_mirror.pl_purchases (
    pl_tx_signature text primary key,
    pl_wallet       text not null,
    pl_package      text,
    pl_coin_amount  integer,
    pl_sol_paid     numeric(20,9),
    pl_cluster      text,
    pl_created_at   timestamptz,
    mirrored_at     timestamptz not null default now()
);

-- ---- Sync heartbeat + completeness watermark (liveness comes from here, NOT
-- ---- from cron.job_run_details which only proves the POST was enqueued).
create table if not exists legacy_mirror.sync_state (
    id                        text primary key,   -- 'gridzero'
    last_run_at               timestamptz,
    last_ok_at                timestamptz,
    legacy_total_players      integer,
    legacy_total_leaderboard  integer,
    legacy_total_purchases    integer,
    rows_upserted_last_run    integer,
    complete_last_run         boolean,            -- rows seen == legacy Content-Range total
    note                      text
);

-- ---- Coins are a SPENDABLE balance, not a high score. The new DB is the sole
-- ---- live authority for coins; a player's legacy coin balance is folded in
-- ---- exactly ONCE (first restore), watermarked here so it can never be
-- ---- re-credited (which would resurrect spent coins / mint free planes).
alter table public.pl_players
    add column if not exists legacy_coins_credited_for text;  -- legacy pl_device_id whose coins were credited once

-- =====================================================================
-- MERGED READ SURFACE (replaces the legacy-only views; same columns/order
-- so the client query is unchanged). definer views (security_invoker off)
-- so anon can read public.* without any grant on legacy_mirror.*
-- =====================================================================

-- Best score per wallet across BOTH DBs. Dedup by the globally-unique on-chain
-- signature with native winning a tie, then distinct-on(wallet) best score —
-- preserving the legacy "Semantic B" the shipped client expects.
create or replace view public.pl_leaderboard_top as
with unioned as (
    select pl_wallet, pl_score, pl_tx_signature, pl_cluster, pl_created_at, 0 as src_rank
    from public.pl_leaderboard
    union all
    select pl_wallet, pl_score, pl_tx_signature, pl_cluster, pl_created_at, 1 as src_rank
    from legacy_mirror.pl_leaderboard
),
by_sig as (
    select distinct on (pl_tx_signature)
        pl_wallet, pl_score, pl_tx_signature, pl_cluster, pl_created_at
    from unioned
    order by pl_tx_signature, src_rank          -- native (0) beats mirror (1) for same signature
)
select distinct on (pl_wallet)
    pl_wallet, pl_score, pl_tx_signature, pl_cluster, pl_created_at
from by_sig
order by pl_wallet, pl_score desc, pl_created_at asc;

grant select on public.pl_leaderboard_top to anon, authenticated;

-- Ops/validation: per-wallet best high score + combined plane ownership across
-- both DBs (coins deliberately excluded — not a mergeable field).
create or replace view public.pl_progress_by_wallet as
with all_rows as (
    select pl_wallet, pl_high_score, pl_planes_owned
    from public.pl_players where pl_wallet is not null
    union all
    select pl_wallet, pl_high_score, pl_planes_owned
    from legacy_mirror.pl_players where pl_wallet is not null
)
select pl_wallet,
       max(pl_high_score)      as best_high_score,
       bit_or(pl_planes_owned) as planes_owned_union,
       count(*)                as row_count
from all_rows
group by pl_wallet;

grant select on public.pl_progress_by_wallet to anon, authenticated;
