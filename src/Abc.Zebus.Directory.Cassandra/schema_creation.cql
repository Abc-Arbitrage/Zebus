-- First create your Keyspace with the replication factor you want

create table IF NOT EXISTS "Peers" (
	"UselessKey" boolean,
	"PeerId" text,
	"EndPoint" text,
	"IsUp" boolean,
	"IsResponding" boolean,
	"IsPersistent" boolean,
	"TimestampUtc" timestamp,
	"HasDebuggerAttached" boolean,
	"StaticSubscriptions" blob,
	PRIMARY KEY("UselessKey", "PeerId")
);

create table IF NOT EXISTS "DynamicSubscriptions" (
	"UselessKey" boolean,
	"PeerId" text,
	"MessageTypeId" text,	
	"SubscriptionBindings" blob,
	PRIMARY KEY("UselessKey", "PeerId", "MessageTypeId")
);