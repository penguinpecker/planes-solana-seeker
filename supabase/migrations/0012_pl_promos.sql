-- Remotely-driven in-game promo popup (review asks, follow-us / tweet CTAs,
-- announcements). The app fetches the single active row and shows a themed
-- popup; flip `active` / edit text / change the CTA from the dashboard to push
-- a new promo with NO app update. A new pl_id makes it show again to everyone
-- (the client remembers the last promo it dismissed).

create table if not exists public.pl_promos (
    pl_id         uuid primary key default gen_random_uuid(),
    active        boolean not null default false,
    pl_title      text not null,
    pl_body       text not null,
    pl_image_url  text,          -- optional banner shown in the popup
    pl_cta_label  text,          -- optional button text, e.g. "Follow us on X"
    pl_cta_url    text,          -- optional https link the button opens
    pl_created_at timestamptz not null default now(),
    pl_updated_at timestamptz not null default now()
);

alter table public.pl_promos enable row level security;

-- Public read (the app pulls the active promo with the anon key); writes are
-- service-role only (dashboard / SQL editor).
create policy pl_promos_read on public.pl_promos for select using (true);

create or replace function public.pl_promos_touch_updated_at()
returns trigger language plpgsql as $$
begin new.pl_updated_at := now(); return new; end $$;

drop trigger if exists pl_promos_touch on public.pl_promos;
create trigger pl_promos_touch before update on public.pl_promos
    for each row execute function public.pl_promos_touch_updated_at();

-- A disabled starter row so the table isn't empty (flip active=true + edit to ship).
insert into public.pl_promos (active, pl_title, pl_body, pl_cta_label, pl_cta_url)
values (false, 'Follow Planes on X', 'Get updates, events and rewards — follow us!', 'Follow us on X', 'https://x.com/planesminiapp')
on conflict do nothing;
