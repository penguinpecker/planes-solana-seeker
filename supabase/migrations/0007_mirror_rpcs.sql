-- SECURITY DEFINER bridge so the mirror edge function (service_role) can
-- write/read the PRIVATE legacy_mirror schema via PostgREST RPC, without ever
-- exposing legacy_mirror to the API. All functions run as the owner (postgres),
-- schema-qualify everything (search_path=''), and are callable ONLY by
-- service_role.

-- ---- bulk upsert: legacy players (mirror faithfully reflects legacy => last-pulled-wins)
create or replace function public.mirror_upsert_players(p jsonb)
returns integer language plpgsql security definer set search_path = '' as $$
declare n integer;
begin
  insert into legacy_mirror.pl_players
    (pl_device_id, pl_wallet, pl_total_coins, pl_high_score, pl_plane_id,
     pl_sound_on, pl_planes_owned, pl_created_at, pl_updated_at, mirrored_at)
  select x.pl_device_id, x.pl_wallet, x.pl_total_coins, x.pl_high_score, x.pl_plane_id,
         x.pl_sound_on, x.pl_planes_owned, x.pl_created_at, x.pl_updated_at, now()
  from jsonb_to_recordset(p) as x(
     pl_device_id text, pl_wallet text, pl_total_coins integer, pl_high_score integer,
     pl_plane_id integer, pl_sound_on boolean, pl_planes_owned integer,
     pl_created_at timestamptz, pl_updated_at timestamptz)
  on conflict (pl_device_id) do update set
     pl_wallet = excluded.pl_wallet, pl_total_coins = excluded.pl_total_coins,
     pl_high_score = excluded.pl_high_score, pl_plane_id = excluded.pl_plane_id,
     pl_sound_on = excluded.pl_sound_on, pl_planes_owned = excluded.pl_planes_owned,
     pl_created_at = excluded.pl_created_at, pl_updated_at = excluded.pl_updated_at,
     mirrored_at = now();
  get diagnostics n = row_count; return n;
end $$;

-- ---- bulk upsert: legacy leaderboard (immutable, signature-keyed)
create or replace function public.mirror_upsert_leaderboard(p jsonb)
returns integer language plpgsql security definer set search_path = '' as $$
declare n integer;
begin
  insert into legacy_mirror.pl_leaderboard
    (pl_tx_signature, pl_wallet, pl_score, pl_cluster, pl_created_at, mirrored_at)
  select x.pl_tx_signature, x.pl_wallet, x.pl_score, x.pl_cluster, x.pl_created_at, now()
  from jsonb_to_recordset(p) as x(
     pl_tx_signature text, pl_wallet text, pl_score integer, pl_cluster text, pl_created_at timestamptz)
  on conflict (pl_tx_signature) do update set
     pl_wallet = excluded.pl_wallet, pl_score = excluded.pl_score,
     pl_cluster = excluded.pl_cluster, pl_created_at = excluded.pl_created_at, mirrored_at = now();
  get diagnostics n = row_count; return n;
end $$;

-- ---- bulk upsert: legacy purchases (immutable, signature-keyed)
create or replace function public.mirror_upsert_purchases(p jsonb)
returns integer language plpgsql security definer set search_path = '' as $$
declare n integer;
begin
  insert into legacy_mirror.pl_purchases
    (pl_tx_signature, pl_wallet, pl_package, pl_coin_amount, pl_sol_paid, pl_cluster, pl_created_at, mirrored_at)
  select x.pl_tx_signature, x.pl_wallet, x.pl_package, x.pl_coin_amount, x.pl_sol_paid, x.pl_cluster, x.pl_created_at, now()
  from jsonb_to_recordset(p) as x(
     pl_tx_signature text, pl_wallet text, pl_package text, pl_coin_amount integer,
     pl_sol_paid numeric, pl_cluster text, pl_created_at timestamptz)
  on conflict (pl_tx_signature) do update set
     pl_wallet = excluded.pl_wallet, pl_package = excluded.pl_package,
     pl_coin_amount = excluded.pl_coin_amount, pl_sol_paid = excluded.pl_sol_paid,
     pl_cluster = excluded.pl_cluster, pl_created_at = excluded.pl_created_at, mirrored_at = now();
  get diagnostics n = row_count; return n;
end $$;

-- ---- record the sync heartbeat / completeness watermark
create or replace function public.mirror_record_state(
  p_players integer, p_leaderboard integer, p_purchases integer,
  p_upserted integer, p_complete boolean, p_note text)
returns void language sql security definer set search_path = '' as $$
  insert into legacy_mirror.sync_state
    (id, last_run_at, last_ok_at, legacy_total_players, legacy_total_leaderboard,
     legacy_total_purchases, rows_upserted_last_run, complete_last_run, note)
  values ('gridzero', now(), case when p_complete then now() else null end,
     p_players, p_leaderboard, p_purchases, p_upserted, p_complete, p_note)
  on conflict (id) do update set
     last_run_at = now(),
     last_ok_at = case when p_complete then now() else legacy_mirror.sync_state.last_ok_at end,
     legacy_total_players = excluded.legacy_total_players,
     legacy_total_leaderboard = excluded.legacy_total_leaderboard,
     legacy_total_purchases = excluded.legacy_total_purchases,
     rows_upserted_last_run = excluded.rows_upserted_last_run,
     complete_last_run = excluded.complete_last_run,
     note = excluded.note;
$$;

-- ---- read-side: best legacy progress for a device + its wallet (for first restore).
-- Returns device-exact coins, wallet-max coins, merged high score, OR'd planes.
-- Coins are returned RAW; the caller (pl-sync-player) decides whether to credit
-- ONCE (watermarked) so spent coins can never be resurrected.
create or replace function public.legacy_restore_for(p_device_id text, p_wallet text)
returns table(device_coins integer, wallet_max_coins integer, best_high_score integer, planes_union integer)
language sql security definer set search_path = '' as $$
  with dev as (
    select pl_total_coins, pl_high_score, pl_planes_owned
    from legacy_mirror.pl_players where pl_device_id = p_device_id
  ),
  wal as (
    select max(pl_total_coins) c, max(pl_high_score) h, bit_or(pl_planes_owned) pw
    from legacy_mirror.pl_players
    where p_wallet is not null and pl_wallet = p_wallet
  )
  select
    (select pl_total_coins from dev),
    (select c from wal),
    greatest(coalesce((select pl_high_score from dev),0), coalesce((select h from wal),0)),
    coalesce((select pl_planes_owned from dev),0) | coalesce((select pw from wal),0);
$$;

-- lock down: only service_role may call these
revoke execute on function public.mirror_upsert_players(jsonb)     from public, anon, authenticated;
revoke execute on function public.mirror_upsert_leaderboard(jsonb) from public, anon, authenticated;
revoke execute on function public.mirror_upsert_purchases(jsonb)   from public, anon, authenticated;
revoke execute on function public.mirror_record_state(integer,integer,integer,integer,boolean,text) from public, anon, authenticated;
revoke execute on function public.legacy_restore_for(text,text)    from public, anon, authenticated;
grant  execute on function public.mirror_upsert_players(jsonb)     to service_role;
grant  execute on function public.mirror_upsert_leaderboard(jsonb) to service_role;
grant  execute on function public.mirror_upsert_purchases(jsonb)   to service_role;
grant  execute on function public.mirror_record_state(integer,integer,integer,integer,boolean,text) to service_role;
grant  execute on function public.legacy_restore_for(text,text)    to service_role;
