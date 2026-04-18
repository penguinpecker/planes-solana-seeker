-- Track plane-ownership flags so a reinstall that reconnects the same wallet
-- can restore previously-purchased planes. Bit N of pl_planes_owned = owns plane N.
-- Bit 0 is always set (the free default plane).
alter table public.pl_players
    add column if not exists pl_planes_owned integer not null default 1;
