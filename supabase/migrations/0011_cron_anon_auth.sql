-- Reschedule the mirror cron to authenticate with the PUBLIC anon key (a
-- client-shipped key, not the sensitive service-role key) plus the mirror
-- secret. Both are seeded into Vault at runtime via seed_cron_secrets() so no
-- secret value ever lives in a migration file / migration history.

select cron.unschedule('mirror-legacy-gridzero')
where exists (select 1 from cron.job where jobname = 'mirror-legacy-gridzero');

select cron.schedule(
  'mirror-legacy-gridzero',
  '*/5 * * * *',
  $job$
  select net.http_post(
    url := 'https://zrqepsqbicswophjzxzn.supabase.co/functions/v1/mirror-legacy',
    headers := jsonb_build_object(
      'Content-Type', 'application/json',
      'Authorization', 'Bearer ' || coalesce((select decrypted_secret from vault.decrypted_secrets where name = 'planes_anon_key'), ''),
      'apikey', coalesce((select decrypted_secret from vault.decrypted_secrets where name = 'planes_anon_key'), ''),
      'x-mirror-secret', coalesce((select decrypted_secret from vault.decrypted_secrets where name = 'planes_mirror_secret'), '')
    ),
    body := '{}'::jsonb,
    timeout_milliseconds := 8000
  );
  $job$
);

-- One-shot Vault seeder: values are supplied at call time (not stored here).
create or replace function public.seed_cron_secrets(p_anon text, p_mirror text)
returns void language plpgsql security definer set search_path = '' as $$
declare sid uuid;
begin
  select id into sid from vault.secrets where name = 'planes_anon_key';
  if sid is null then perform vault.create_secret(p_anon, 'planes_anon_key');
  else perform vault.update_secret(sid, p_anon); end if;

  select id into sid from vault.secrets where name = 'planes_mirror_secret';
  if sid is null then perform vault.create_secret(p_mirror, 'planes_mirror_secret');
  else perform vault.update_secret(sid, p_mirror); end if;
end $$;

revoke execute on function public.seed_cron_secrets(text,text) from public, anon, authenticated;
grant  execute on function public.seed_cron_secrets(text,text) to service_role;
