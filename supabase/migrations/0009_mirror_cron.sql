-- Schedule the legacy mirror to run every 5 minutes.
--
-- pg_cron fires pg_net to POST the mirror-legacy edge function. Credentials are
-- read from Vault BY NAME (planes_service_key, planes_mirror_secret) so no
-- secret value lives in this migration. Populate those two Vault secrets once
-- (Supabase Dashboard > SQL Editor) and the job authenticates itself:
--   select vault.create_secret('<SERVICE_ROLE_KEY>', 'planes_service_key');
--   select vault.create_secret('<MIRROR_SECRET>',    'planes_mirror_secret');
-- Until they exist the job harmlessly posts empty auth (function returns 401).

create extension if not exists pg_cron;
create extension if not exists pg_net with schema extensions;

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
      'Authorization', 'Bearer ' || coalesce((select decrypted_secret from vault.decrypted_secrets where name = 'planes_service_key'), ''),
      'apikey', coalesce((select decrypted_secret from vault.decrypted_secrets where name = 'planes_service_key'), ''),
      'x-mirror-secret', coalesce((select decrypted_secret from vault.decrypted_secrets where name = 'planes_mirror_secret'), '')
    ),
    body := '{}'::jsonb,
    timeout_milliseconds := 8000
  );
  $job$
);
