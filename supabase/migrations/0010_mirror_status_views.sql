-- Read-only ops views so mirror health + the cron registration are observable
-- via the API (no secrets exposed; cron command column is NOT selected).

create or replace view public.mirror_sync_status as
select id, last_run_at, last_ok_at,
       legacy_total_players, legacy_total_leaderboard, legacy_total_purchases,
       rows_upserted_last_run, complete_last_run,
       (now() - last_run_at) as since_last_run, note
from legacy_mirror.sync_state;
grant select on public.mirror_sync_status to anon, authenticated;

create or replace view public.mirror_cron_status as
select jobid, jobname, schedule, active
from cron.job where jobname = 'mirror-legacy-gridzero';
grant select on public.mirror_cron_status to anon, authenticated;
