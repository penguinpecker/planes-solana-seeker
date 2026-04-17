create table if not exists public.pl_purchases (
    pl_id            uuid primary key default gen_random_uuid(),
    pl_wallet        text not null,
    pl_package       text not null check (pl_package in ('coins_1000','coins_2000','coins_3000')),
    pl_coin_amount   integer not null check (pl_coin_amount >= 0),
    pl_sol_paid      numeric(20,9) not null check (pl_sol_paid >= 0),
    pl_tx_signature  text not null unique,
    pl_cluster       text not null default 'mainnet-beta' check (pl_cluster in ('devnet','mainnet-beta','testnet')),
    pl_created_at    timestamptz not null default now()
);

create index if not exists pl_purchases_wallet_idx
    on public.pl_purchases (pl_wallet, pl_created_at desc);

create index if not exists pl_purchases_created_idx
    on public.pl_purchases (pl_created_at desc);

alter table public.pl_purchases enable row level security;

create policy pl_purchases_read
    on public.pl_purchases for select
    using (true);

create or replace view public.pl_purchases_by_wallet as
select
    pl_wallet,
    count(*)                           as purchases,
    sum(pl_coin_amount)                as total_coins,
    sum(pl_sol_paid)                   as total_sol,
    max(pl_created_at)                 as last_purchase
from public.pl_purchases
group by pl_wallet
order by total_sol desc;

create or replace view public.pl_purchases_daily as
select
    date_trunc('day', pl_created_at) as day,
    count(*)                          as purchases,
    sum(pl_coin_amount)               as coins_sold,
    sum(pl_sol_paid)                  as sol_revenue
from public.pl_purchases
group by 1
order by 1 desc;
