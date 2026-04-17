create table if not exists public.pl_leaderboard (
    pl_id           uuid primary key default gen_random_uuid(),
    pl_wallet       text not null,
    pl_score        integer not null check (pl_score >= 0),
    pl_tx_signature text not null,
    pl_cluster      text not null default 'devnet' check (pl_cluster in ('devnet','mainnet-beta','testnet')),
    pl_created_at   timestamptz not null default now(),
    unique (pl_tx_signature)
);

create index if not exists pl_leaderboard_score_desc_idx
    on public.pl_leaderboard (pl_score desc, pl_created_at asc);

create index if not exists pl_leaderboard_wallet_idx
    on public.pl_leaderboard (pl_wallet);

alter table public.pl_leaderboard enable row level security;

create policy pl_leaderboard_read
    on public.pl_leaderboard for select
    using (true);

-- Best score per wallet view (what the leaderboard UI queries).
create or replace view public.pl_leaderboard_top as
select distinct on (pl_wallet)
    pl_wallet,
    pl_score,
    pl_tx_signature,
    pl_cluster,
    pl_created_at
from public.pl_leaderboard
order by pl_wallet, pl_score desc, pl_created_at asc;
