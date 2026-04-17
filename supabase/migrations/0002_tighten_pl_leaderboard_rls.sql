-- Anon role must no longer be able to insert — only the service role
-- (used by the pl-submit-score edge function after on-chain verification)
-- is allowed to write rows now.
drop policy if exists pl_leaderboard_insert on public.pl_leaderboard;
