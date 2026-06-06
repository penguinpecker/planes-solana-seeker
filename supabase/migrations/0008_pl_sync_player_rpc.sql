-- Atomic player-sync merge for the dual-DB world.
--
-- Preserves the legacy never-shrink semantics WITHIN this DB (coins/high MAX,
-- planes OR across the device row + the patch + all other rows for the wallet),
-- and folds the LEGACY (mirror) progress in correctly:
--   * high_score / planes_owned are monotonic -> always MAX/OR in legacy values.
--   * coins are a SPENDABLE balance -> legacy coins are credited EXACTLY ONCE
--     per wallet (or per device for anonymous players), watermarked in
--     legacy_coin_credits. After that the new DB is the sole coin authority, so
--     spending here can never be resurrected by the still-running mirror.
-- Everything runs in one transaction (the function body), so the credit-claim
-- and the upsert commit or roll back together.

create table if not exists public.legacy_coin_credits (
    credit_key  text primary key,          -- wallet, or 'device:<id>' when anonymous
    credited_at timestamptz not null default now()
);
alter table public.legacy_coin_credits enable row level security;  -- no policy => service_role only

create or replace function public.pl_sync_player(p jsonb)
returns jsonb language plpgsql security definer set search_path = '' as $$
declare
  v_device  text    := p->>'pl_device_id';
  v_pwallet text    := nullif(p->>'pl_wallet','');
  v_pcoins  int     := case when jsonb_typeof(p->'pl_total_coins')='number' then (p->>'pl_total_coins')::int end;
  v_phigh   int     := case when jsonb_typeof(p->'pl_high_score')='number'  then (p->>'pl_high_score')::int  end;
  v_pplane  int     := case when jsonb_typeof(p->'pl_plane_id')='number'    then (p->>'pl_plane_id')::int    end;
  v_psound  boolean := case when jsonb_typeof(p->'pl_sound_on')='boolean'   then (p->>'pl_sound_on')::boolean end;
  v_powned  int     := case when jsonb_typeof(p->'pl_planes_owned')='number' then (p->>'pl_planes_owned')::int end;
  r_existing  public.pl_players%rowtype;
  v_wallet    text;
  v_prior_coins int; v_prior_high int; v_prior_owned int; v_prior_plane int; v_prior_sound boolean;
  v_lg        record;
  v_claimed   boolean := false;
  v_coins int; v_high int; v_owned int; v_plane int; v_sound boolean;
  r_out       public.pl_players%rowtype;
begin
  if v_device is null or length(v_device) < 8 or length(v_device) > 128 then
    raise exception 'invalid pl_device_id';
  end if;

  select * into r_existing from public.pl_players where pl_device_id = v_device;
  v_wallet := coalesce(v_pwallet, r_existing.pl_wallet);

  -- Best across every OTHER native row for this wallet (cross-device, this DB).
  if v_wallet is not null then
    select max(pl_total_coins), max(pl_high_score), bit_or(pl_planes_owned)
      into v_prior_coins, v_prior_high, v_prior_owned
      from public.pl_players where pl_wallet = v_wallet and pl_device_id <> v_device;
    select pl_plane_id, pl_sound_on into v_prior_plane, v_prior_sound
      from public.pl_players where pl_wallet = v_wallet and pl_device_id <> v_device
      order by pl_total_coins desc nulls last limit 1;
  end if;

  -- Legacy (mirror) progress for this device + wallet.
  select * into v_lg from public.legacy_restore_for(v_device, v_wallet);

  -- Coins credited from legacy exactly once (per wallet, else per device).
  insert into public.legacy_coin_credits(credit_key)
    values (coalesce(v_wallet, 'device:'||v_device))
    on conflict (credit_key) do nothing;
  v_claimed := found;  -- true only when this call actually inserted (first credit)

  v_coins := greatest(coalesce(r_existing.pl_total_coins,0), coalesce(v_pcoins,0), coalesce(v_prior_coins,0));
  if v_claimed then
    v_coins := greatest(v_coins, coalesce(v_lg.device_coins,0), coalesce(v_lg.wallet_max_coins,0));
  end if;

  v_high  := greatest(coalesce(r_existing.pl_high_score,0), coalesce(v_phigh,0),
                      coalesce(v_prior_high,0), coalesce(v_lg.best_high_score,0));
  v_owned := (coalesce(r_existing.pl_planes_owned,0) | coalesce(v_powned,0)
              | coalesce(v_prior_owned,0) | coalesce(v_lg.planes_union,0) | 1) & 2147483647;
  v_plane := coalesce(v_pplane, r_existing.pl_plane_id, v_prior_plane, 0);
  v_sound := coalesce(v_psound, r_existing.pl_sound_on, v_prior_sound, true);

  insert into public.pl_players
    (pl_device_id, pl_wallet, pl_total_coins, pl_high_score, pl_plane_id, pl_sound_on, pl_planes_owned)
  values (v_device, v_wallet, v_coins, v_high, v_plane, v_sound, v_owned)
  on conflict (pl_device_id) do update set
    pl_wallet       = coalesce(excluded.pl_wallet, public.pl_players.pl_wallet),
    pl_total_coins  = excluded.pl_total_coins,
    pl_high_score   = excluded.pl_high_score,
    pl_plane_id     = excluded.pl_plane_id,
    pl_sound_on     = excluded.pl_sound_on,
    pl_planes_owned = excluded.pl_planes_owned
  returning * into r_out;

  return to_jsonb(r_out);
end $$;

revoke execute on function public.pl_sync_player(jsonb) from public, anon, authenticated;
grant  execute on function public.pl_sync_player(jsonb) to service_role;
