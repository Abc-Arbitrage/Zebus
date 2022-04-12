-- First create your Keyspace with the replication factor you want

create table IF NOT EXISTS "Peers_2" (
	"PeerId" text,
	"EndPoint" text,
	"IsUp" boolean,
	"IsResponding" boolean,
	"IsPersistent" boolean,
	"TimestampUtc" timestamp,
	"HasDebuggerAttached" boolean,
	"StaticSubscriptions" blob,
	PRIMARY KEY("PeerId")
);

create table IF NOT EXISTS "DynamicSubscriptions_2" (
	"PeerId" text,
	"MessageTypeId" text,
	"SubscriptionBindings" blob,
	PRIMARY KEY("PeerId", "MessageTypeId")
);
