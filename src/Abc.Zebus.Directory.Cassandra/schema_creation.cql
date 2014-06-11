-- First create your Keyspace with the replication factor you want

create table IF NOT EXISTS "Peers" (
	"PeerId" text PRIMARY KEY,
	"EndPoint" text,
	"IsUp" boolean,
	"IsResponding" boolean,
	"IsPersistent" boolean,
	"TimestampUtc" timestamp,
	"HasDebuggerAttached" boolean,
	"StaticSubscriptions" blob
);

create table IF NOT EXISTS "DynamicSubscriptions" (
	"PeerId" text,
	"SubscriptionIdentifier" uuid,
	"MessageTypeFullName" text,
	"BindingKeyParts" map<int, text>,
	PRIMARY KEY("PeerId", "SubscriptionIdentifier")
);