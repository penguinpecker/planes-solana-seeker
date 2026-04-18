-- Per-install player row, keyed by a client-generated UUID.
create table if not exists public.pl_players (
    pl_id           uuid primary key default gen_random_uuid(),
    pl_device_id    text not null unique,
    pl_wallet       text,
    pl_total_coins  integer not null default 0,
    pl_high_score   integer not null default 0,
    pl_plane_id     integer not null default 0,
    pl_sound_on     boolean not null default true,
    pl_created_at   timestamptz not null default now(),
    pl_updated_at   timestamptz not null default now()
);

create index if not exists pl_players_wallet_idx
    on public.pl_players (pl_wallet) where pl_wallet is not null;

alter table public.pl_players enable row level security;

create policy pl_players_read
    on public.pl_players for select
    using (true);

create or replace function public.pl_players_touch_updated_at()
returns trigger as $$
begin
    new.pl_updated_at := now();
    return new;
end;
$$ language plpgsql;

drop trigger if exists pl_players_touch on public.pl_players;
create trigger pl_players_touch
    before update on public.pl_players
    for each row
    execute function public.pl_players_touch_updated_at();
