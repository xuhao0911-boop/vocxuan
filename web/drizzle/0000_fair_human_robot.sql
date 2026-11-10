CREATE TABLE `progress_state` (
	`owner_id` text PRIMARY KEY NOT NULL,
	`revision` integer NOT NULL,
	`payload` blob NOT NULL,
	`receipts` text DEFAULT '[]' NOT NULL,
	`updated_at` text NOT NULL
);
