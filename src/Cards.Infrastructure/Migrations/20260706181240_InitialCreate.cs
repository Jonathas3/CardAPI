using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cards.Infrastructure.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pin_access_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accessed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ip = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pin_access_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cardholder_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nickname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    brand = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    card_number_encrypted = table.Column<string>(type: "text", nullable: false),
                    card_number_first4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    card_number_last4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    pin_encrypted = table.Column<string>(type: "text", nullable: false),
                    expiration_date = table.Column<DateOnly>(type: "date", nullable: false),
                    credit_limit = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cards", x => x.id);
                    table.CheckConstraint("ck_cards_credit_limit", "credit_limit >= 0");
                    table.CheckConstraint("ck_cards_status", "status IN (0, 1, 2)");
                    table.ForeignKey(
                        name: "FK_cards_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    replaced_by_session_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cards_user_id_expiration_date",
                table: "cards",
                columns: new[] { "user_id", "expiration_date" });

            migrationBuilder.CreateIndex(
                name: "IX_cards_user_id_is_deleted_created_at",
                table: "cards",
                columns: new[] { "user_id", "is_deleted", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_pin_access_logs_card_id_accessed_at",
                table: "pin_access_logs",
                columns: new[] { "card_id", "accessed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sessions_user_id_expires_at",
                table: "sessions",
                columns: new[] { "user_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.Sql(@"
INSERT INTO users (id, name, username, password_hash, created_at) VALUES
('11111111-1111-1111-1111-111111111111', 'Mariana Alves', 'mariana.alves', '$2b$11$AiHwe7MAD4wlsTH6W1wJiuvjf4MJ6jT7XIS.ie40FEGwjY4JVeVUq', now() - interval '90 days'),
('22222222-2222-2222-2222-222222222222', 'Carlos Silva', 'carlos.silva', '$2b$11$dLEdFMyI56gpeWz2tbHzeOE823CVo7CkwyTIP/WlivyFg2RmHHsne', now() - interval '60 days');

INSERT INTO cards (id, user_id, cardholder_name, nickname, brand, card_number_encrypted, card_number_first4, card_number_last4, pin_encrypted, expiration_date, credit_limit, status, created_at, updated_at, is_deleted) VALUES
('bd026f40-ba19-469e-aef8-56d2ba27db67', '11111111-1111-1111-1111-111111111111', 'MARIANA ALVES', 'Principal', 'VISA', 'zN/MY/6s20TVB9ZHZh33qkAdeY7t0VQJFIt6E+6BTTd/mYH0aLqPfbrXp+g=', '5309', '2424', 'y/bkJN8lOVNVx9y9FWjDf7BSZtpPOk5e7pDC7RU+YwI=', '2027-01-28', 9016.51, 0, '2026-06-01T09:00:00', '2026-06-01T09:00:00', false),
('a9772c39-f69d-4e88-a3a5-177b9b4ca472', '11111111-1111-1111-1111-111111111111', 'MARIANA ALVES', 'Cartao Eventos', 'MASTERCARD', 'XahJsx0B2/G/0rnMjBXZSKswXfBmRqSioy7cSfiKhwxOM76+YSioWLA9fCc=', '4061', '9928', 'nC4G3N5le0GBkfYVMv/uoErVISLwc/c4ZxTMUzg0IcY=', '2028-02-28', 5188.37, 0, '2026-06-01T12:00:00', '2026-06-01T12:00:00', false),
('4f04be57-a6af-4ad6-b013-9b96371c69a6', '11111111-1111-1111-1111-111111111111', 'MARIANA ALVES', 'Reserva', 'ELO', 'uH1hjw3Ah078d9BQz3W8IyCBTH7kZ9mHUer5reWaI2glXyUjZszMMnU2hcM=', '5206', '6514', 'qGiqdbY9w7Gd48iSSlnC2IlZYRbecMv7WnXt0jGla4g=', '2029-03-28', 2762.17, 0, '2026-06-01T15:00:00', '2026-06-01T15:00:00', false),
('4afd738e-2b98-4281-b37f-856055169854', '11111111-1111-1111-1111-111111111111', 'MARIANA ALVES', 'Viagens', 'VISA', 'g+Jv4dEjZAwJnzyFrSB/e2+F81EVrQYTKRcXbYBeHGHBkNTtrsxnvNzF71w=', '4198', '7201', 'sNFt6XQo0RwDHBW9Ub1EwUSANqh8AnZJtbvq+dlAprE=', '2030-04-28', 11488.77, 1, '2026-06-01T18:00:00', '2026-06-01T18:00:00', false),
('27ba24ee-25bc-4ec7-8588-603a2535d1e2', '11111111-1111-1111-1111-111111111111', 'MARIANA ALVES', 'Compras Online', 'MASTERCARD', '+3h0gfs2IhV1ZfiNTIDqspz1r4sBKCoHVSjQ1VFRsO1KLPCcpBKyyhQr8MM=', '5698', '2307', 'hebKjFKG435jUn6qCVQSE2JT4urfCLPtlZYC7eUdGKE=', '2027-05-28', 17463.19, 0, '2026-06-01T21:00:00', '2026-06-01T21:00:00', false),
('0a11c426-2d67-4954-9dfe-3b5e532cb742', '11111111-1111-1111-1111-111111111111', 'MARIANA ALVES', 'Assinaturas', 'ELO', 'iF6OtL+Yzic0uvCx5sEngbeD1eyFnsJ0gsAy4Y9Fg1+z0XSLx1oBXU3VRsI=', '4778', '2169', '1ABCTY8+18npkXglxV5p4kq3Bt4xkjTg977c1NNh8Js=', '2028-06-28', 13064.59, 0, '2026-06-02T00:00:00', '2026-06-02T00:00:00', false),
('ebadab40-63ac-4a96-8d96-b29239fb25c7', '11111111-1111-1111-1111-111111111111', 'MARIANA ALVES', 'Emergencia', 'VISA', 'yehd2fj89F+kCN3so7sU6UIXyb7ShYviv+W0hNSl6571c+UqE4u0xpPdkGg=', '5093', '1916', 'GhiaDi3Ri1NZ+3ol5sGaveXYJc7UURDpHCZ+n+MlS08=', '2029-07-28', 16614.25, 0, '2026-06-02T03:00:00', '2026-06-02T03:00:00', false),
('e5fd0437-b374-4cf1-a93c-4093a1e98e09', '11111111-1111-1111-1111-111111111111', 'MARIANA ALVES', 'Uso diario', 'MASTERCARD', 'lfQt+TygIjnAm3Q/E4mYQFaJnq1Ba/cugfonJGruydYieKwa46gx6roA9xk=', '5648', '9179', 'zvSHELeD2B/UNbaaisosd7ECInNrC0H8aGn9bIIykLs=', '2030-08-28', 17808.98, 1, '2026-06-02T06:00:00', '2026-06-02T06:00:00', false),
('d9bb616b-fd12-413d-964f-28dad13569cc', '11111111-1111-1111-1111-111111111111', 'MARIANA ALVES', 'Cartao Extra 1', 'ELO', '9NMNol/tPFV/7s2SF6n8dJ7jAIzCXFXwihRjFYWGypH+fZMCmT62dOwjFSQ=', '5316', '8019', 'xMSvdibBZGLkf2u21AFnmE44kNvHUqco39RFUN6Fy6Q=', '2027-09-28', 8588.61, 0, '2026-06-02T09:00:00', '2026-06-02T09:00:00', false),
('c9f70b06-03ed-48c8-b99a-d5560f6ef985', '11111111-1111-1111-1111-111111111111', 'MARIANA ALVES', 'Cartao Extra 2', 'VISA', 'RQk/SxzdLl0eq+B8Ky6CA2SaAURmpcMYtoCA0IRhcdhH9NpNaGD9zdw4iyM=', '4449', '7916', 'Bhec1RP68Xaf0DI1HcUalGgrECqEKwKmlBQYl7t3tCo=', '2028-10-28', 2207.03, 0, '2026-06-02T12:00:00', '2026-06-02T12:00:00', false),
('53044693-626b-4b89-b7a6-e6f4aa14b8c7', '11111111-1111-1111-1111-111111111111', 'MARIANA ALVES', 'Cartao Extra 3', 'MASTERCARD', 'mpM7od60IOkdFiIonMKuQYzyc+QzfWLoXS1KNiU43j5Z8DrPwVrvkpZ//6M=', '4781', '5371', 'WW/PGzXtdPTfO5OHJcKdcsOYrph/oQdFFytYSWviLpA=', '2029-11-28', 3119.49, 0, '2026-06-02T15:00:00', '2026-06-02T15:00:00', false),
('614e5630-0649-4afa-a518-49c4c3bc74d0', '11111111-1111-1111-1111-111111111111', 'MARIANA ALVES', 'Cartao Extra 4', 'ELO', 'wOpS28r5ZPTzLzT1m+8LAuoslRBkfwULDbj45KeS+omVppQdHDnnLN2+yx8=', '4890', '5889', 'eTGloVEcFCi2gevjW33RMeePOpy6A24J2MKoGDASjhA=', '2030-12-28', 12570.43, 1, '2026-06-02T18:00:00', '2026-06-02T18:00:00', false),
('c81f3f5b-319d-47e5-a484-fe51e1d4ff2a', '22222222-2222-2222-2222-222222222222', 'CARLOS SILVA', 'Principal', 'VISA', 'SkElqR6lOP73ztGKqsp1sY4fpbBTVx/c2jFdv1Kwty7/RSqbGlSWFpvjk9c=', '4313', '1319', 'yQyuAXMi8v1aELEZb4cGn1ZLa/HbeOFWTKHaiHmzhBU=', '2028-01-15', 14078.28, 0, '2026-06-02T09:00:00', '2026-06-02T09:00:00', false),
('d33b459d-d63f-4aa0-a440-c34c7979b705', '22222222-2222-2222-2222-222222222222', 'CARLOS SILVA', 'Reserva', 'MASTERCARD', 'shlgTUDLsEAO7r7W4qonQwvPJe9G1B0CvcqpAqhjds/3tMMJGuOfk1TUSCI=', '5799', '2133', 'HipJGQiHHnlVPV/kOApN7jUUOwHU8QrEejk/aBiZeoU=', '2028-02-15', 11955.85, 0, '2026-06-02T14:00:00', '2026-06-02T14:00:00', false),
('c6b843c2-cb5c-4bc7-a55e-cd299d201ccf', '22222222-2222-2222-2222-222222222222', 'CARLOS SILVA', 'Viagens', 'ELO', '5vOSdzrayuebrYStjXSQltn/n/cmqltcp+Kb59/wVm+eN16YCaC5+KO1b3E=', '4262', '9835', 'yvozgKHBeDTSg3vaNZCGL9xqJeAuKCrhu4jvr4Yk4zQ=', '2028-03-15', 11268.34, 0, '2026-06-02T19:00:00', '2026-06-02T19:00:00', false);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cards");

            migrationBuilder.DropTable(
                name: "pin_access_logs");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}

